using System;
using System.Collections.Generic;

namespace DocuTrack.Core.Models
{
    public class Document
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public DocumentStatus Status { get; set; }
        public Guid? OwnerId { get; set; }
        public User? Owner { get; set; }
        public List<RoutingEvent> RoutingHistory { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? FileUrl { get; set; }
        public string? FileName { get; set; }
        public DateTime? DueDate { get; set; }
    }
}