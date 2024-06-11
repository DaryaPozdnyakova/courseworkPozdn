using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace bookstore.Models
{
    public class CartItem
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public int BookId { get; set; }

        public Book Book { get; set; }
    }
}
