namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// Os dados de um tipo de ato de teste, com valores neutros (nenhuma consequência ligada,
/// vigência aberta desde 1/1/2026); o teste declara só o que o cenário exige. É o único ponto
/// dos testes que conhece a assinatura de <c>TipoAtoPublicado.Criar</c> e <c>Atualizar</c>.
/// </summary>
internal sealed record TipoAtoPublicadoDeTeste
{
    public string? Codigo { get; init; } = "EDITAL_ABERTURA";

    public string? Nome { get; init; } = "Edital de abertura";

    public bool CongelaConfiguracao { get; init; }

    public bool UnicoPorObjeto { get; init; }

    public bool EfeitoIrreversivel { get; init; }

    public bool EhResultado { get; init; }

    public bool PublicavelComoAvulso { get; init; }

    public bool ExigeAtoCorrigido { get; init; }

    public DateOnly VigenciaInicio { get; init; } = new(2026, 1, 1);

    public DateOnly? VigenciaFim { get; init; }

    public string? BaseLegal { get; init; }

    public Result<TipoAtoPublicado> Criar() =>
        TipoAtoPublicado.Criar(
            Codigo, Nome, CongelaConfiguracao, UnicoPorObjeto, EfeitoIrreversivel, EhResultado,
            VigenciaInicio, VigenciaFim, BaseLegal, PublicavelComoAvulso, ExigeAtoCorrigido);

    public Result Atualizar(TipoAtoPublicado tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return tipo.Atualizar(
            Codigo, Nome, CongelaConfiguracao, UnicoPorObjeto, EfeitoIrreversivel, EhResultado,
            VigenciaInicio, VigenciaFim, BaseLegal, PublicavelComoAvulso, ExigeAtoCorrigido);
    }
}
