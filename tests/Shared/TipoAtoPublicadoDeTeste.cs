namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Os dados de um tipo de ato de teste, com valores neutros (nenhuma consequência ligada,
/// vigência aberta desde 1/1/2026); o teste declara só o que o cenário exige. Os atributos e o início da
/// vigência são anuláveis, como na declaração, para o teste poder omiti-los. É o único ponto
/// dos testes que conhece a assinatura de <c>TipoAtoPublicado.Criar</c> e <c>Atualizar</c>.
/// </summary>
internal sealed record TipoAtoPublicadoDeTeste
{
    public string? Codigo { get; init; } = "EDITAL_ABERTURA";

    public string? Nome { get; init; } = "Edital de abertura";

    public bool? CongelaConfiguracao { get; init; } = false;

    public bool? UnicoPorObjeto { get; init; } = false;

    public bool? EfeitoIrreversivel { get; init; } = false;

    public bool? EhResultado { get; init; } = false;

    public bool? PublicavelComoAvulso { get; init; } = false;

    public bool? ExigeAtoCorrigido { get; init; } = false;

    public DateOnly? VigenciaInicio { get; init; } = new(2026, 1, 1);

    public DateOnly? VigenciaFim { get; init; }

    public string? BaseLegal { get; init; }

    public DeclaracaoDoTipoAtoPublicado Declaracao() => new(
        Codigo, Nome, CongelaConfiguracao, UnicoPorObjeto, EfeitoIrreversivel, EhResultado,
        PublicavelComoAvulso, ExigeAtoCorrigido, VigenciaInicio, VigenciaFim, BaseLegal);

    public Result<TipoAtoPublicado> Criar() => TipoAtoPublicado.Criar(Declaracao());

    public Result Atualizar(TipoAtoPublicado tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return tipo.Atualizar(Declaracao());
    }
}
