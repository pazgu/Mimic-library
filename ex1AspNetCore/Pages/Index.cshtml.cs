
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;

namespace ex1AspNetCore.Pages
{
    public class IndexModel : PageModel
    {
        private const string FilePath = "books.csv";

        [BindProperty]
        public Book Book { get; set; }

        [BindProperty]
        public string searchTerm { get; set; }

        [BindProperty]
        public string BookTitle { get; set; }

        [BindProperty]
        public string BookAuthor { get; set; }

        public Dictionary<int, Book> Books = new Dictionary<int, Book>();
        public List<Book> SearchResults { get; set; } 
        public bool SearchError { get; set; }

        private readonly string _cacheKey = "BooksCache";

        private readonly IMemoryCache _cache;
        public IndexModel(IMemoryCache cache)
        {
            _cache = cache;
        }
        public void OnGet()
        {
            Books = ReadBooksFromFile();
            _cache.Set<Dictionary<int, Book>>(_cacheKey, Books); //storing the books in the cache
        }

        private Dictionary<int, Book> ReadBooksFromFile()
        {
            var books = new Dictionary<int, Book>();
            using (var reader = new StreamReader(FilePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();

                while (csv.Read())
                {
                    if (csv.TryGetField<int>("bookID", out int id)
                        && csv.TryGetField("title", out string title)
                        && csv.TryGetField("authors", out string authors))
                        //returns true if the parsing is successful
                    {
                        Book book = new Book
                        {
                            bookID = id,
                            title = title,
                            authors = authors,
                        };

                        books.Add(id, book);
                    }
                }
            }
            return books;
        }

        public void OnPostAdd()
        {
            try
            {
                AddBook();
                ViewData["MessageAdd"] = "New book added successfuly";
                // allows to pass data from the controller to the view
            }
            catch (Exception ex)
            {
                ViewData["MessageAdd"] = $"Adding new book was failed due to {ex}";
            }
        }

        private void AddBook()
        {
            Books = _cache.Get<Dictionary<int, Book>>(_cacheKey);
            int last_index = Books.Keys.Last(); //saves the last key added 
            Book newBook = new Book
            {
                bookID = last_index + 1, //so each new book will get the next key value (I found it redundant to let the user insert id) 
                title = BookTitle,
                authors = BookAuthor
            };
            Books.Add(newBook.bookID, newBook);
            _cache.Set(_cacheKey, Books);

            using (StreamWriter writer = new StreamWriter(FilePath, true))
            {
                string newRow = $"{newBook.bookID}, {newBook.title}, {newBook.authors}";
                writer.WriteLine(newRow);
            }
        }

        public void OnPostSearch()
        {
            SearchResults = SearchBook();    
        }

        private List<Book> SearchBook()
        {
            Dictionary<int, Book> books = _cache.Get<Dictionary<int, Book>>(_cacheKey);
            List<Book> results = new List<Book>();

            if (string.IsNullOrEmpty(searchTerm) || !IsValidSearchTerm(searchTerm))
            {
                SearchError = true;
                return results;
            }

            foreach (Book book in books.Values)
            {
                if (book.title.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    book.bookID.ToString().Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
                //specifies case-insensitive comparison
                {
                    results.Add(book);
                }
            }

            return results;
        }

        private bool IsValidSearchTerm(string searchTerm)
        {
            // the first condition checks if the searchTerm is a single digit

            return Regex.IsMatch(searchTerm, @"^\d$") || Regex.IsMatch(searchTerm, @"^[a-zA-Z0-9\s]*$");

            //the second consition checks if the searchTerm contains only letters, digits, and spaces
        }

        public void OnPostDelete()
        {
            bool status = DeleteBook();
            if (status)
            {
                ViewData["MessageDelete"] = "Book deleted successfuly";
            }
            else
            {
                ViewData["MessageDelete"] = "There is no such book available. Please try again.";
            }
        }

        private bool DeleteBook()
        {
            Books = _cache.Get<Dictionary<int, Book>>(_cacheKey);
            if (int.TryParse(BookId, out int bookIdValue) && Books.Remove(bookIdValue))
            //The Remove method returns a boolean indicating whether the removal was successful
            {
                using (StreamWriter writer = new StreamWriter(FilePath, false))
                {
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        csv.WriteRecords(Books.Values);
                    }
                }
                _cache.Set(_cacheKey, Books);
                return true;
            }
            return false;
        }
    }
}

