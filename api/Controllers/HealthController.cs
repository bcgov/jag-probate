using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Probate.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        [AllowAnonymous]
        public ActionResult<object> Get()
        {
            //want to return "Healthy at time.now"
            return Ok(string.Concat("Healthy - ", System.DateTime.Now.ToString("o")));
        }
    }
}
