using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ImageFilter.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private static HttpClient client = new HttpClient();

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        internal class SnapshotErrorResponse
        {
            public object Error { get; set; }
            public bool Success { get; set; }
        }

        public async Task<IActionResult> WebApi(string account, string passwd)
        {
            //var s = HttpContext.Request.Path + HttpContext.Request.QueryString;
            return await GetImage(account, passwd);
        }

        public IActionResult ViewSnapshot()
        {
            return View();
        }

        public async Task<IActionResult> GetImage(string username, string password)
        {
            const string serverName = "http://192.168.1.108:5000";

            var snapshotUrl = $"{serverName}/webapi/entry.cgi?camStm=1&version=2&cameraId=3&api=%22SYNO.SurveillanceStation.Camera%22&method=GetSnapshot";

            var response = await client.GetAsync(snapshotUrl);
            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync();

                try
                {
                    var error = JsonConvert.DeserializeObject<SnapshotErrorResponse>(html);
                    if (!error.Success)
                    {
                        var authUrl = $"{serverName}/webapi/auth.cgi?api=SYNO.API.Auth&method=Login&version=1&account={username}&passwd={password}&session=SurveillanceStation";
                        response = await client.GetAsync(authUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            return await GetImage(username, password);
                        }
                    }
                }
                catch (JsonException)
                { }

                using (var image = await Image.LoadAsync(await response.Content.ReadAsStreamAsync()))
                {
                    var polygon = new Polygon(new LinearLineSegment(
                        new PointF(0, 0),
                        new PointF((float)(image.Width * 0.65), 0),
                        new PointF((float)(image.Width * 0.68), 180),
                        new PointF((float)(image.Width * 0.73), 180),
                        new PointF((float)(image.Width * 0.78), (float)(image.Height * 0.45)),
                        new PointF((float)(image.Width * 0.30), (float)(image.Height * 0.60)),
                        new PointF(0, (float)(image.Height * 0.83))
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

            return null;
        }
    }
}
