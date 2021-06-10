using ImageFilter.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ImageFilter.Controllers
{
    public class ImageFilterController : Controller
    {
        private readonly ILogger<ImageFilterController> _logger;
        private static HttpClient client = new HttpClient();

        private IMemoryCache _cache;

        private const string _serverName = "http://192.168.1.108:5000";

        public ImageFilterController(ILogger<ImageFilterController> logger, IMemoryCache memoryCache)
        {
            _logger = logger;
            _cache = memoryCache;
        }

        internal class SnapshotErrorResponse
        {
            public object Error { get; set; }
            public bool Success { get; set; }
        }

        public ActionResult Index()
        {
            return View(new CredentialsViewModel());
        }

        //[Route("[controller]/SaveCredentials")]
        public async Task<ActionResult> SaveCredentials(CredentialsViewModel credentials)
        {
            await Authenticate(credentials.Username, credentials.Password);

            return RedirectToAction(nameof(Index), "ImageFilter");
        }

        [Route("WebApi/auth.cgi")]
        public async Task<JsonResult> Authenticate(string account, string passwd)
        {
            if (account == null)
                account = "";
            if (passwd == null)
                passwd = "";

            _cache.Set("username", account);
            _cache.Set("password", passwd);

            var authUrl = $"{_serverName}/webapi/auth.cgi?api=SYNO.API.Auth&method=Login&version=1&account={account}&passwd={passwd}&session=SurveillanceStation";
            var response = await client.GetAsync(authUrl);
            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync();
                return Json(JsonConvert.DeserializeObject<SnapshotErrorResponse>(html));
            }

            return Json(new { success = false });
        }

        public IActionResult ViewSnapshot()
        {
            return View();
        }

        [Route("WebApi/entry.cgi")]
        public async Task<IActionResult> GetImage(int cameraId, bool allowRecursion = true)
        {
            var snapshotUrl = $"{_serverName}/webapi/entry.cgi?camStm=1&version=2&cameraId={cameraId}&api=%22SYNO.SurveillanceStation.Camera%22&method=GetSnapshot";

            var response = await client.GetAsync(snapshotUrl);
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    using (var image = await Image.LoadAsync(await response.Content.ReadAsStreamAsync()))
                    {
                        var polygon = new Polygon(new LinearLineSegment(
                            new PointF(0, 0),
                            new PointF((float)(image.Width * 0.76), 0),
                            new PointF((float)(image.Width * 0.79), 400),
                            new PointF((float)(image.Width * 0.83), 430),
                            new PointF((float)(image.Width * 0.82), 840),
                            new PointF((float)(image.Width * 0.83), 870),
                            new PointF((float)(image.Width * 0.83), 990),
                            new PointF((float)(image.Width * 0.31), 1030),
                            new PointF(0, 1335)
                            ));

                        image.Mutate(x => x.Fill(Color.Black, polygon));

                        using (var stream = new MemoryStream())
                        {
                            await image.SaveAsJpegAsync(stream);

                            var content = stream.ToArray();

                            return File(content, "image/jpeg", "snapshot.jpeg");
                        }
                    }
                }
                catch
                {
                    if (allowRecursion)
                    {
                        await Authenticate((string)_cache.Get("username"), (string)_cache.Get("password"));
                        return await GetImage(cameraId, false);
                    }
                }
            }
            else if (allowRecursion)
            {
                await Authenticate((string)_cache.Get("username"), (string)_cache.Get("password"));
                return await GetImage(cameraId, false);
            }

            return null;
        }
    }
}
