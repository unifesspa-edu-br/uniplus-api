namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O modelo como a simulação do administrador o interpreta: o conteúdo do modelo aplicado — o de inscrição,
/// com o conjunto básico —, com o catálogo vivo: as opções que o catálogo oferta a cada campo, completadas
/// pelo cadastro institucional, e os agregados sobre os grupos. O renderizável que o front interpreta e a
/// pré-visualização partem dela, para darem o mesmo resultado à mesma simulação.
/// </summary>
internal sealed record SimulacaoDoModelo(
    ConteudoDoModelo Conteudo,
    IReadOnlyList<FatoCandidato> Fatos,
    IReadOnlyDictionary<string, IReadOnlyList<ValorSelecionavel>?> Opcoes,
    IReadOnlyList<DefinicaoAgregado> Agregados)
{
    /// <summary>Os códigos que cada campo de seleção oferta, das opções da simulação.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Ofertas { get; } = VocabularioDoCatalogo.Ofertas(Opcoes);

    public static async Task<SimulacaoDoModelo> MontarAsync(
        ModeloFormulario modelo,
        IReadOnlyList<FatoCandidato> fatos,
        OpcoesDoCadastroInstitucional cadastro,
        CancellationToken cancellationToken)
    {
        ConteudoDoModelo conteudo = ConjuntoBasicoDoModelo.ComoAplicado(modelo, fatos);
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = VocabularioDoCatalogo.OpcoesDoModelo(fatos, conteudo);
        await cadastro.CompletarAsync(opcoes, fatos, cancellationToken).ConfigureAwait(false);
        return new SimulacaoDoModelo(conteudo, fatos, opcoes, VocabularioDoCatalogo.AgregadosDosGrupos(fatos, conteudo.Grupos));
    }

    /// <summary>
    /// A definição do formulário que o avaliador interpreta, com as derivações por regra do catálogo e os
    /// agregados sobre os grupos.
    /// </summary>
    public DefinicaoFormulario Definicao() => Conteudo.ParaAvaliacao(VocabularioDoCatalogo.DerivacoesDosFatos(Fatos), Agregados);

    /// <summary>
    /// A resposta fora das opções que o catálogo oferta não vale, como no renderizável que o front
    /// interpreta.
    /// </summary>
    public Dictionary<string, JsonElement> RespostasDentroDaOferta(IReadOnlyDictionary<string, JsonElement> respostas) =>
        RespostaDeCampo.DentroDaOferta(respostas, Ofertas);
}
