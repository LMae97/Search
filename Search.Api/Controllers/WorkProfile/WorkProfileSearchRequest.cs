using Search.Application.Querying;

namespace Search.Api.Controllers.WorkProfile;

public class WorkProfileSearchRequest : SearchRequest
{
    public string? Search { get; set; } = null; 
}

//TODO: RAGIONARE SULLA VALIDAZIONE DEI FILTRI DI BASE