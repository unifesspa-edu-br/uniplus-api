namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As distribuições de vagas do rascunho com os cadastros vivos que elas citam, lidos uma única vez: os nomes e o quadro de
/// vagas da exportação saem do mesmo resultado.
/// </summary>
internal sealed class VagasDoRascunhoComCadastrosVivos(DeclaracoesDoDocumento declaracoes, CadastrosCitadosNoDocumento cadastros)
{
    internal IReadOnlyList<IDescricaoDeCadastro> DescricoesDosNomes() =>
    [
        new DescricaoDeOfertasDeCurso(cadastros.VagasEExigencias.OfertasDeCurso),
        new DescricaoDeModalidades(cadastros.VagasEExigencias.Modalidades),
    ];

    /// <summary>As distribuições que os cadastros vivos permitem calcular, com a posição de cada uma na lista do documento.</summary>
    internal IReadOnlyList<(int PosicaoNoDocumento, ConfiguracaoDistribuicaoVagas Distribuicao)> DistribuicoesCalculadas() =>
        VagasLidas.Ler(declaracoes, cadastros).DistribuicoesNaPosicaoDoDocumento();
}
