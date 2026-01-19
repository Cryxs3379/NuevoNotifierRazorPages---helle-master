using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NotifierAPI.Services;
using NotifierAPI.Models;
using NotifierAPI.Data;

namespace NotifierAPI.Pages.Messages;

public class MessagesIndexModel : PageModel
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ILogger<MessagesIndexModel> _logger;

    public MessagesIndexModel(NotificationsDbContext dbContext, ILogger<MessagesIndexModel> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string Direction { get; set; } = "inbound";

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public string? AccountRef { get; set; }

    public MessagesResponse? Messages { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalPages => Messages != null && PageSize > 0 
        ? (int)Math.Ceiling((double)Messages.Total / PageSize) 
        : 0;
    
    // Propiedad para la vista (alias de CurrentPage)
    public int PageNumber => CurrentPage;

    public async Task OnGetAsync()
    {
        try
        {
            // Validar dirección
            if (Direction != "inbound" && Direction != "outbound")
            {
                Direction = "inbound";
            }

            // Validar página
            if (CurrentPage < 1) CurrentPage = 1;

            // Validar tamaño de página
            if (PageSize < 10) PageSize = 10;
            if (PageSize > 200) PageSize = 200;

            // Mapear Direction a byte
            byte directionByte = Direction == "inbound" ? (byte)0 : (byte)1;

            _logger.LogInformation("Fetching messages from SQL: direction={Direction} (byte={DirectionByte}), page={Page}, pageSize={PageSize}", 
                Direction, directionByte, CurrentPage, PageSize);

            // Consultar SQL
            var query = _dbContext.SmsMessages.AsNoTracking()
                .Where(m => m.Direction == directionByte)
                .OrderByDescending(m => m.MessageAt)
                .ThenByDescending(m => m.Id);

            // Contar total
            var totalCount = await query.CountAsync(HttpContext.RequestAborted);

            // Aplicar paginación
            var skip = (CurrentPage - 1) * PageSize;
            var dbMessages = await query
                .Skip(skip)
                .Take(PageSize)
                .ToListAsync(HttpContext.RequestAborted);

            // Mapear a MessageDto
            var mappedItems = dbMessages.Select(m => new MessageDto
            {
                Id = m.Id.ToString(),
                From = m.Originator,
                To = m.Recipient,
                Message = m.Body,
                ReceivedUtc = m.MessageAt,
                SentBy = m.SentBy // Incluir SentBy (trabajador que responde)
            }).ToList();

            Messages = new MessagesResponse
            {
                Items = mappedItems,
                Page = CurrentPage,
                PageSize = PageSize,
                Total = totalCount
            };

            _logger.LogInformation("Retrieved {Count} messages out of {Total} from SQL", 
                mappedItems.Count, 
                totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading messages from SQL");
            ErrorMessage = "Ocurrió un error al cargar los mensajes desde la base de datos.";
        }
    }
}

