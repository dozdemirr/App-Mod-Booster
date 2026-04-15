using AppModAssist.Models;
using AppModAssist.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppModAssist.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(ErrorContextStore errorStore) : ControllerBase
{
    [HttpGet("error")]
    public ActionResult<UiErrorDetails?> GetError()
    {
        return Ok(errorStore.Get());
    }
}
