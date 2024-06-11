using System.Collections.Generic;

using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace bookstore.Models
{
    public class ProfileViewModel
    {
        public User User { get; set; }
        public List<Book> FavoriteBooks { get; set; }
        public List<Order> Orders { get; set; }

     
        public string Email { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime? BirthDate { get; set; }
        public IFormFile Avatar { get; set; }
    }
}

