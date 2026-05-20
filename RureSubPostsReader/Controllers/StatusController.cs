using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace RureSubPostsReader.Controllers;

public class StatusController : Controller
{
    public IActionResult Index()
    {
        return Ok("Posts reader service is working!");
    }
}
