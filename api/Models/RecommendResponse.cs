using System.Text.Json.Serialization;

namespace api.Models;

/// <summary>POST /api/recommend response — exact shape from the project rules.</summary>
public class RecommendResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "recommendation";

    [JsonPropertyName("resource_name")]
    public string? ResourceName { get; set; }

    [JsonPropertyName("building")]
    public string? Building { get; set; }

    [JsonPropertyName("room")]
    public string? Room { get; set; }

    [JsonPropertyName("is_online")]
    public bool? IsOnline { get; set; }

    [JsonPropertyName("online_link")]
    public string? OnlineLink { get; set; }

    [JsonPropertyName("next_open")]
    public string? NextOpen { get; set; }

    [JsonPropertyName("session_type")]
    public string? SessionType { get; set; }

    [JsonPropertyName("contact_email")]
    public string? ContactEmail { get; set; }

    [JsonPropertyName("contact_phone")]
    public string? ContactPhone { get; set; }

    [JsonPropertyName("booking_url")]
    public string? BookingUrl { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("what_to_expect")]
    public string? WhatToExpect { get; set; }

    [JsonPropertyName("last_verified")]
    public string? LastVerified { get; set; }

    [JsonPropertyName("is_stale")]
    public bool IsStale { get; set; }

    [JsonPropertyName("show_syllabus_tip")]
    public bool ShowSyllabusTip { get; set; }

    [JsonPropertyName("declined_assignment_help")]
    public bool DeclinedAssignmentHelp { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
