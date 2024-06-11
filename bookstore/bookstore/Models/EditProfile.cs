
using System.Diagnostics.CodeAnalysis;

namespace bookstore.Models
{
    public class EditProfile
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? BirthDate { get; set; }

    }

}

