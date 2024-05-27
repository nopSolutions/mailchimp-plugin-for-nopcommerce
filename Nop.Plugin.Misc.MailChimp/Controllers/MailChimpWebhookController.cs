using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Caching;
using Nop.Plugin.Misc.MailChimp.Services;

namespace Nop.Plugin.Misc.MailChimp.Controllers;

public class MailChimpWebhookController : Controller
{
    #region Fields

    private readonly IStaticCacheManager _staticCacheManager;
    private readonly MailChimpManager _mailChimpManager;

    #endregion

    #region Ctor

    public MailChimpWebhookController(IStaticCacheManager staticCacheManager,
        MailChimpManager mailChimpManager)
    {
        _staticCacheManager = staticCacheManager;
        _mailChimpManager = mailChimpManager;
    }

    #endregion

    #region Methods

    public IActionResult BatchWebhook()
    {
        return Ok();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> BatchWebhook(IFormCollection form)
    {
        if (!Request.Form?.Any() ?? true)
            return BadRequest();

        //try to get already handled batches
        var batchesInfo = await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.SynchronizationBatchesCacheKey), () => new Dictionary<string, int>());

        //handle batch webhook
        var (id, completedOperationNumber) = await _mailChimpManager.HandleBatchWebhookAsync(Request.Form, batchesInfo);
        if (!string.IsNullOrEmpty(id) && completedOperationNumber.HasValue)
        {
            if (!batchesInfo.ContainsKey(id))
            {
                //update cached value
                batchesInfo.Add(id, completedOperationNumber.Value);
                await _staticCacheManager.SetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.SynchronizationBatchesCacheKey), batchesInfo);
            }
            return Ok();
        }
        return BadRequest();
    }

    public IActionResult WebHook()
    {
        return Ok();
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> WebHook(IFormCollection form)
    {
        if (!Request.Form?.Any() ?? true)
            return BadRequest();

        //handle webhook
        var success = await _mailChimpManager.HandleWebhookAsync(Request.Form);
        return success ? Ok() : BadRequest();
    }

    #endregion
}
