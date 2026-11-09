using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AIWork.Web.Controllers;
[AllowAnonymous]
public class SystemController:Controller
{
    [HttpGet]public IActionResult Version()=>Json(new{application="AIWorkAssistant",version="1.1.1",experience="dashboard-ai-entry-streaming"});
}
