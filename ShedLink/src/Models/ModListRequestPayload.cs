namespace ShedLink.Models;

/// <summary>
/// The dashboard sends this (empty) to ask for a fresh mod listing.
/// It's intentionally blank — the message type alone is enough to trigger the scan.
/// </summary>
public class ModListRequestPayload { }
