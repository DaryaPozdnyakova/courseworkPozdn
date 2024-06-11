using bookstore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace bookstore.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Catalog(string searchString)
        {
            var books = from b in _context.Books select b;

            if (!string.IsNullOrEmpty(searchString))
            {
                books = books.Where(s => s.Title.Contains(searchString) || s.NameAuthor.Contains(searchString));
            }

            return View(await books.ToListAsync());
        }

        public async Task<IActionResult> BookDetails(int id)
        {
            var book = await _context.Books
                                     .Include(b => b.FavoriteBooks)
                                     .FirstOrDefaultAsync(b => b.Id == id);
            if (book == null)
            {
                return NotFound();
            }
            return View(book);
        }


        private bool IsInCart(int id)
        {
            List<int> cart = HttpContext.Session.GetObjectFromJson<List<int>>("Cart") ?? new List<int>();
            return cart.Contains(id);
        }

        [HttpPost]
        public IActionResult AddToCart(int id)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Пользователь не авторизован. Пожалуйста, войдите в свою учетную запись." });
            }

            List<int> cart = HttpContext.Session.GetObjectFromJson<List<int>>("Cart") ?? new List<int>();
            if (!cart.Contains(id))
            {
                cart.Add(id);
            }
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult RemoveFromCart(int id)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Пользователь не авторизован. Пожалуйста, войдите в свою учетную запись." });
            }

            List<int> cart = HttpContext.Session.GetObjectFromJson<List<int>>("Cart") ?? new List<int>();
            if (cart.Contains(id))
            {
                cart.Remove(id);
            }
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            return Json(new { success = true });
        }

        public IActionResult Cart()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<int>>("Cart") ?? new List<int>();
            var cartItems = _context.Books.Where(b => cart.Contains(b.Id)).ToList();
            return View(cartItems);
        }

        [HttpPost]
        public IActionResult ClearCart()
        {
            HttpContext.Session.SetObjectAsJson("Cart", new List<int>());
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> AddToFavorites(int bookId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Вы не авторизованы" });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Json(new { success = false, message = "Not authenticated" });
            }

            var userId = int.Parse(userIdClaim.Value);
            var user = await _context.Users.Include(u => u.FavoriteBooks).SingleOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            if (!user.FavoriteBooks.Any(fb => fb.BookId == bookId))
            {
                var favorite = new FavoriteBook
                {
                    UserId = userId,
                    BookId = bookId
                };

                _context.FavoriteBooks.Add(favorite);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromFavorites(int bookId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Вы не авторизованы" });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Json(new { success = false, message = "Вы не авторизованы" });
            }

            var userId = int.Parse(userIdClaim.Value);
            var favorite = await _context.FavoriteBooks.SingleOrDefaultAsync(fb => fb.UserId == userId && fb.BookId == bookId);

            if (favorite != null)
            {
                _context.FavoriteBooks.Remove(favorite);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Книга не найдена в избранном." });
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

