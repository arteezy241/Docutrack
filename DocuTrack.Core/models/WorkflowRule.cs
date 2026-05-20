namespace DocuTrack.Core.Models
{
    public class WorkflowRule
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // When a document reaches this status...
        public DocumentStatus TriggerStatus { get; set; }

        // ...automatically route it to this user
        public Guid? AssignToUserId { get; set; }
        public User? AssignToUser { get; set; }

        // ...and set this new status   
        public DocumentStatus NextStatus { get; set; }

        // Optional note to attach to the routing event
        public string? Note { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}