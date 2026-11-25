namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Uma página do modelo: uma seção com itens, ou um bloco que o sistema monta.</summary>
public sealed record PaginaDoModelo(
    string Codigo, int Ordem, TipoPaginaFormulario Tipo, BlocoSistema? Bloco, string Titulo, string? Descricao, string? Aviso, PredicadoDnf? Exibicao)
{
    public IReadOnlyCollection<string> FatosCitados => Exibicao?.FatosCitados ?? [];

    /// <summary>A exibição da página, com o caminho do campo dela na entrada.</summary>
    public IReadOnlyList<PredicadoNaEntrada> Predicados() => [new("exibicao", Exibicao)];
}
