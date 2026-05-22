using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocuTrack.Core.Models
{
    public class RoutingEvent
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Guid? FromUserId { get; set; }
        public Guid? ToUserId { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Note { get; set; }
        public DocumentStatus? StatusAfter { get; set; }
        [ForeignKey("DocumentId")]
        public Document? Document { get; set; }
    }
}
