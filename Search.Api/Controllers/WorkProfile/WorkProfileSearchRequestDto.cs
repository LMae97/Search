using Search.Api.Dto;
using Search.Core;

namespace Search.Api.Controllers.WorkProfile;

/// <summary>DTO di ricerca per WorkProfile. <c>Search</c> (free-text) è ereditato da <see cref="SearchRequest"/>.</summary>
public class WorkProfileSearchRequestDto : BaseSearchRequestDto
{
}

//TODO: RAGIONARE SULLA VALIDAZIONE DEI FILTRI DI BASE