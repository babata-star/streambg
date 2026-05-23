using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StreamBG.API.Services;
using StreamBG.Core.Entities;
using StreamBG.Infrastructure.Data;

namespace StreamBG.API.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subs;
    private string UserId => User.FindFirst("sub")?.Value ?? string.Empty;

    public SubscriptionsController(ISubscriptionService subs) => _subs = subs;

    /// <summary>Планове на даден стриймър (публично)</summary>
    [HttpGet("plans/{creatorUsername}")]
    public async Task<IActionResult> GetPlans(string creatorUsername,
        [FromServices] StreamBGDbContext db)
    {
        var creator = await db.Users.FirstOrDefaultAsync(u => u.Username == creatorUsername);
        if (creator is null) return NotFound();
        return Ok(await _subs.GetCreatorPlansAsync(creator.Id));
    }

    /// <summary>Мои абонаментни планове (стриймър)</summary>
    [HttpGet("my/plans")]
    [Authorize]
    public async Task<IActionResult> GetMyPlans() =>
        Ok(await _subs.GetCreatorPlansAsync(UserId));

    /// <summary>Създаване на план</summary>
    [HttpPost("my/plans")]
    [Authorize(Policy = "StreamerOnly")]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest req)
    {
        try { return Ok(await _subs.CreatePlanAsync(UserId, req)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Обновяване на план</summary>
    [HttpPut("my/plans/{planId:int}")]
    [Authorize(Policy = "StreamerOnly")]
    public async Task<IActionResult> UpdatePlan(int planId, [FromBody] CreatePlanRequest req)
    {
        try { return Ok(await _subs.UpdatePlanAsync(planId, UserId, req)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Изтриване на план</summary>
    [HttpDelete("my/plans/{planId:int}")]
    [Authorize(Policy = "StreamerOnly")]
    public async Task<IActionResult> DeletePlan(int planId)
    {
        await _subs.DeletePlanAsync(planId, UserId);
        return Ok();
    }

    /// <summary>Абонирай се за план</summary>
    [HttpPost("subscribe/{planId:int}")]
    [Authorize]
    public async Task<IActionResult> Subscribe(int planId)
    {
        var result = await _subs.StartSubscriptionAsync(UserId, planId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Отказ от абонамент</summary>
    [HttpPost("cancel/{subscriptionId:int}")]
    [Authorize]
    public async Task<IActionResult> Cancel(int subscriptionId)
    {
        await _subs.CancelSubscriptionAsync(UserId, subscriptionId);
        return Ok(new { message = "Абонаментът е отказан. Ще изтече в края на периода." });
    }

    /// <summary>Моите активни абонаменти</summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> Mine() =>
        Ok(await _subs.GetMySubscriptionsAsync(UserId));

    /// <summary>Моите абонати (стриймър)</summary>
    [HttpGet("my/subscribers")]
    [Authorize(Policy = "StreamerOnly")]
    public async Task<IActionResult> MySubscribers([FromQuery] int page = 1) =>
        Ok(await _subs.GetMySubscribersAsync(UserId, page));

    /// <summary>Приходи на стриймъра</summary>
    [HttpGet("my/earnings")]
    [Authorize(Policy = "StreamerOnly")]
    public async Task<IActionResult> Earnings(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddMonths(-1);
        var t = to ?? DateTime.UtcNow;
        return Ok(await _subs.GetEarningsAsync(UserId, f, t));
    }

    /// <summary>Проверка дали е абониран (за UI)</summary>
    [HttpGet("check/{creatorUsername}")]
    [Authorize]
    public async Task<IActionResult> Check(string creatorUsername,
        [FromServices] StreamBGDbContext db)
    {
        var creator = await db.Users.FirstOrDefaultAsync(u => u.Username == creatorUsername);
        if (creator is null) return NotFound();
        var isSubbed = await _subs.IsSubscribedAsync(UserId, creator.Id);
        var badge = await _subs.GetSubscriberBadgeAsync(UserId, creator.Id);
        return Ok(new { isSubscribed = isSubbed, badge });
    }

    /// <summary>Stripe webhook (плащания)</summary>
    [HttpPost("webhook/stripe")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook()
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync();
        var sig = Request.Headers["Stripe-Signature"].ToString();
        var ok = await _subs.HandleStripeWebhookAsync(payload, sig);
        return ok ? Ok() : BadRequest();
    }
}
