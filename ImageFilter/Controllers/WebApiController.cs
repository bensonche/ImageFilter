using Microsoft.AspNetCore.Mvc;
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
    public class WebApiController : Controller
    {
        private readonly ILogger<WebApiController> _logger;
        private static HttpClient client = new HttpClient();

        private const string _serverName = "http://192.168.1.108:5000";

        public WebApiController(ILogger<WebApiController> logger)
        {
            _logger = logger;
        }

        internal class SnapshotErrorResponse
        {
            public object Error { get; set; }
            public bool Success { get; set; }
        }

        [Route("[controller]/auth.cgi")]
        public async Task<JsonResult> Authenticate(string account, string passwd)
        {
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

        [Route("[controller]/entry.cgi")]
        public async Task<IActionResult> GetImage(int cameraId)
        {
            var snapshotUrl = $"{_serverName}/webapi/entry.cgi?camStm=1&version=2&cameraId={cameraId}&api=%22SYNO.SurveillanceStation.Camera%22&method=GetSnapshot";

            var response = await client.GetAsync(snapshotUrl);
            if (response.IsSuccessStatusCode)
            {
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
