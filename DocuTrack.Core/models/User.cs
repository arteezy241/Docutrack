using System;

namespace DocuTrack.Core.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public Guid? DepartmentId { get; set; }
        public Department? Department { get; set; }
    }
}