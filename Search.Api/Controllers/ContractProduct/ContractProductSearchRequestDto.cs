using WeByte.Search.Api.Controllers.Contract;

namespace WeByte.Search.Api.Controllers.ContractProduct;

/// <summary>
/// Stessi filtri di <see cref="SearchContractRequestDto"/> (i prodotti appartengono ai contratti, non hanno
/// filtri propri distinti oggi): eredita invece di duplicare i campi, così <see cref="Contract.ContractRequestAdapter"/>
/// può tradurla direttamente, senza un adapter dedicato.
/// </summary>
public sealed class ContractProductSearchRequestDto : SearchContractRequestDto
{
}
