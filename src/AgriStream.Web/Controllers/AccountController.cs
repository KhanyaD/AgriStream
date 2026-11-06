using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace AgriStream.Web.Controllers;
public sealed class AccountController(SqlAccountStore store):Controller
{
    [AllowAnonymous] public async Task<IActionResult> Login(){ if(!store.IsConfigured)return View("SetupRequired"); if(await store.CountAsync()==0)return RedirectToAction(nameof(Setup)); return View(); }
    [HttpPost,AllowAnonymous,ValidateAntiForgeryToken] public async Task<IActionResult> Login(LoginModel model)
    {
        if(!ModelState.IsValid)return View(model); var user=await store.FindAsync(model.Username);
        if(user is null||!store.Verify(user,model.Password)){ModelState.AddModelError("","Invalid username or password.");return View(model);}
        var claims=new[]{new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),new Claim(ClaimTypes.Name,user.Username),new Claim(ClaimTypes.Role,user.Role)};
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)));
        return RedirectToAction("Index","Home");
    }
    [AllowAnonymous] public async Task<IActionResult> Setup(){if(!store.IsConfigured)return View("SetupRequired");if(await store.CountAsync()>0)return RedirectToAction(nameof(Login));return View();}
    [HttpPost,AllowAnonymous,ValidateAntiForgeryToken] public async Task<IActionResult> Setup(SetupModel model)
    {
        if(await store.CountAsync()>0)return RedirectToAction(nameof(Login)); if(!ModelState.IsValid)return View(model);
        try{await store.CreateAsync(model.Username,model.Password,"Admin");return RedirectToAction(nameof(Login));}catch(Exception e){ModelState.AddModelError("",e.Message);return View(model);}
    }
    [Authorize(Roles="Admin")] public IActionResult CreateUser()=>View();
    [HttpPost,Authorize(Roles="Admin"),ValidateAntiForgeryToken] public async Task<IActionResult> CreateUser(CreateUserModel model)
    {if(!ModelState.IsValid)return View(model);try{await store.CreateAsync(model.Username,model.Password,model.Role);TempData["Message"]="User created.";return RedirectToAction("Index","Home");}catch(Exception e){ModelState.AddModelError("",e.Message);return View(model);}}
    [Authorize,HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Logout(){await HttpContext.SignOutAsync();return RedirectToAction(nameof(Login));}
    [AllowAnonymous] public IActionResult Denied()=>View();
}
