using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace bookstore.Models
{
    public class RegisterViewModel
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime BirthDate { get; set; }
        public string FullName { get; set; }
        public IFormFile Avatar { get; set; }
    }

}
