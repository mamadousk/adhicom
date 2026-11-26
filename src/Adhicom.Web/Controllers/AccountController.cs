using System.Security.Claims;
using Adhicom.Application.Notifications;
using Adhicom.Domain.Common;
using Adhicom.Infrastructure.Identity;
using Adhicom.Application.Common;
using Adhicom.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Adhicom.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailSender _email;
    private readonly INotificationService _notifications;
    private readonly ILogger<AccountController> _logger;
    private readonly IWebHostEnvironment _env;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        IEmailSender email, INotificationService notifications, ILogger<AccountController> logger, IWebHostEnvironment env)
        => (_userManager, _signInManager, _email, _notifications, _logger, _env) = (userManager, signInManager, email, notifications, logger, env);

    [HttpGet("/account/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost("/account/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is not null && !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Ce compte est désactivé. Contactez le support Adhicom.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            _logger.LogInformation("Connexion réussie pour {Email}", model.Email);
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
            if (user is not null && await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin)) return Redirect("/admin");
            return Redirect("/app");
        }
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Compte verrouillé suite à trop de tentatives. Réessayez dans 10 minutes.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Identifiants invalides.");
        return View(model);
    }

    [HttpGet("/account/register")]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel());
    }

    [HttpPost("/account/register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Cette adresse e-mail est déjà utilisée.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        if (!await _userManager.IsInRoleAsync(user, AppRoles.Merchant))
            await _userManager.AddToRoleAsync(user, AppRoles.Merchant);

        await _signInManager.SignInAsync(user, isPersistent: false);
        await _notifications.NotifyAsync(Guid.Parse(user.Id), null, "welcome", "Bienvenue sur Adhicom 🎉",
            "Votre compte est créé. Créez maintenant votre boutique en quelques minutes.", "/app/stores/create");

        _logger.LogInformation("Nouvelle inscription : {Email}", model.Email);
        return Redirect("/app/stores/create");
    }

    [HttpPost("/account/logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }

    [HttpGet("/account/forgot-password")]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost("/account/forgot-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        // Réponse identique dans tous les cas : ne pas divulguer l'existence d'un compte
        if (user is not null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var link = Url.Action(nameof(ResetPassword), "Account", new { email = model.Email, token }, Request.Scheme);
            await _email.SendAsync(model.Email, "Réinitialisation du mot de passe — Adhicom",
                $"Bonjour,\n\nPour réinitialiser votre mot de passe, ouvrez le lien suivant :\n{link}\n\nSi vous n'êtes pas à l'origine de cette demande, ignorez cet e-mail.");
            if (_env.IsDevelopment()) ViewData["DevResetLink"] = link; // commodité locale uniquement
        }

        ViewData["Submitted"] = true;
        return View(model);
    }

    [HttpGet("/account/reset-password")]
    public IActionResult ResetPassword(string email, string token)
        => View(new ResetPasswordViewModel { Email = email, Token = token });

    [HttpPost("/account/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null) return View("ResetPasswordConfirmation"); // ne pas divulguer

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (result.Succeeded) return View("ResetPasswordConfirmation");

        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [HttpGet("/account/access-denied")]
    public IActionResult AccessDenied() => View();
}

