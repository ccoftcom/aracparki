using System.Text.Json;
using AracParki.Application.Listings;
using Microsoft.Extensions.Logging;

namespace AracParki.Web.Pages.IlanVer;

public static class WizardDraftStore
{
    public const string SessionKey = "ilan-ver-draft";
    public const string ChoiceKey = "ilan-ver-draft-choice";
    public const string ChoiceContinue = "continue";
    public const string ChoiceNew = "new";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static WizardDraft Get(ISession session)
    {
        var raw = session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new WizardDraft();
        }

        try
        {
            return JsonSerializer.Deserialize<WizardDraft>(raw, JsonOptions) ?? new WizardDraft();
        }
        catch (JsonException)
        {
            return new WizardDraft();
        }
    }

    public static WizardDraft FromJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new WizardDraft();
        }

        try
        {
            return JsonSerializer.Deserialize<WizardDraft>(raw, JsonOptions) ?? new WizardDraft();
        }
        catch (JsonException)
        {
            return new WizardDraft();
        }
    }

    public static string ToJson(WizardDraft draft)
        => JsonSerializer.Serialize(draft, JsonOptions);

    public static void Save(ISession session, WizardDraft draft)
        => session.SetString(SessionKey, ToJson(draft));

    public static void Clear(ISession session) => session.Remove(SessionKey);

    public static string? GetChoice(ISession session)
        => session.GetString(ChoiceKey);

    public static void SetChoice(ISession session, string choice)
        => session.SetString(ChoiceKey, choice);

    public static void ClearChoice(ISession session)
        => session.Remove(ChoiceKey);

    public static async Task PersistAsync(
        ISession session,
        IWizardDraftStore store,
        long? accountId,
        WizardDraft draft,
        CancellationToken cancellationToken)
    {
        Save(session, draft);
        if (accountId is null)
        {
            return;
        }

        await store.SaveAsync(accountId.Value, draft.Step, ToJson(draft), cancellationToken);
    }

    public static async Task ClearAllAsync(
        ISession session,
        IWizardDraftStore store,
        long? accountId,
        CancellationToken cancellationToken)
    {
        Clear(session);
        ClearChoice(session);
        if (accountId is not null)
        {
            await store.ClearAsync(accountId.Value, cancellationToken);
        }
    }

    /// <summary>
    /// Hard-deletes the wizard draft row, session, and uploaded media blobs.
    /// Skips blob deletion when the draft is editing an existing listing
    /// (<see cref="WizardDraft.EditingAdNo"/>) so live listing images stay intact.
    /// Use this for "new listing / discard draft"; use <see cref="ClearAllAsync"/> after publish.
    /// </summary>
    public static async Task DiscardHardAsync(
        ISession session,
        IWizardDraftStore store,
        IListingImageStorage imageStorage,
        long? accountId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var draft = Get(session);
        if (accountId is not null)
        {
            var (dbDraft, meta) = await PeekDbDraftAsync(store, accountId.Value, cancellationToken);
            if (meta is not null)
            {
                draft = dbDraft;
            }
        }

        if (string.IsNullOrWhiteSpace(draft.EditingAdNo))
        {
            await DeleteDraftMediaAsync(draft, imageStorage, logger, cancellationToken);
        }

        await ClearAllAsync(session, store, accountId, cancellationToken);
    }

    private static async Task DeleteDraftMediaAsync(
        WizardDraft draft,
        IListingImageStorage imageStorage,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in draft.ImageAssets)
        {
            if (ListingImageUrl.TryResolveStorageKey(asset, asset.DeliveryUrl, out var key))
            {
                keys.Add(key);
            }
        }

        foreach (var url in draft.ImageUrls)
        {
            if (ListingImageUrl.TryResolveStorageKey(null, url, out var key))
            {
                keys.Add(key);
            }
        }

        foreach (var storageKey in keys)
        {
            try
            {
                await imageStorage.DeleteAsync(storageKey, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Hard-delete failed for draft media {StorageKey}", storageKey);
            }
        }
    }

    /// <summary>
    /// Choice-aware load. Does not silently hydrate DB into session —
    /// call <see cref="HydrateFromDbAsync"/> after user chooses Continue.
    /// </summary>
    public static async Task<WizardDraft> LoadAsync(
        ISession session,
        IWizardDraftStore store,
        long? accountId,
        CancellationToken cancellationToken)
    {
        var choice = GetChoice(session);
        var sessionDraft = Get(session);

        if (accountId is null)
        {
            return sessionDraft;
        }

        if (string.Equals(choice, ChoiceContinue, StringComparison.Ordinal)
            || string.Equals(choice, ChoiceNew, StringComparison.Ordinal))
        {
            return sessionDraft;
        }

        // No choice yet: do not auto-resume from DB (modal decides).
        return sessionDraft;
    }

    public static async Task<(WizardDraft Draft, WizardDraftMeta? Meta)> PeekDbDraftAsync(
        IWizardDraftStore store,
        long accountId,
        CancellationToken cancellationToken)
    {
        var meta = await store.GetMetaAsync(accountId, cancellationToken);
        if (meta is null || string.IsNullOrWhiteSpace(meta.PayloadJson))
        {
            return (new WizardDraft(), meta);
        }

        return (FromJson(meta.PayloadJson), meta);
    }

    public static async Task<WizardDraft> HydrateFromDbAsync(
        ISession session,
        IWizardDraftStore store,
        long accountId,
        CancellationToken cancellationToken)
    {
        var (draft, _) = await PeekDbDraftAsync(store, accountId, cancellationToken);
        Save(session, draft);
        SetChoice(session, ChoiceContinue);
        return draft;
    }
}
