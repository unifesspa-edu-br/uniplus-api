namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Restauracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioProcessoSeletivoRestaurarConfiguracao;

/// <summary>
/// A validação da árvore restaurada acumula as causas independentes e nunca lança: o que depende de dado já recusado (nó de Id
/// repetido, folha com filhos, folha sem documento) não é reconstruído nem lido, e o resto segue conferido, na ordem das
/// famílias e, dentro de cada uma, na posição.
/// </summary>
public sealed class ValidacaoDaArvoreRestauradaTests
{
    private const string IdDuplicado = "ProcessoSeletivo.IdNoExigenciaDuplicado";
    private const string OrdemDeRaizDuplicada = "ProcessoSeletivo.OrdemRaizNoExigenciaDuplicada";
    private const string FolhaComFilhos = "ProcessoSeletivo.NoExigenciaFolhaComFilhos";
    private const string QuantidadeMinimaAusente = "ProcessoSeletivo.NoExigenciaQuantidadeMinimaAusente";
    private const string FolhaSemDocumento = "ProcessoSeletivo.NoExigenciaFolhaSemDocumentoExigido";

    [Fact(DisplayName = "Com as três irregularidades no mesmo grafo, as causas independentes saem juntas na ordem das famílias, sem lançar")]
    public void TresIrregularidades_DevolvemOLoteSemLancar()
    {
        FaseCronograma fase = FaseConforme();
        DocumentoExigido[] documentos = Documentos(fase, 6);
        Guid repetido = Guid.CreateVersion7();
        NoExigencia comFilhos = ComFilhos(2, documentos[2], Folha(Guid.CreateVersion7(), 0, documentos[3]));
        NoExigencia[] nos =
        [
            Folha(repetido, 0, documentos[0]), Folha(repetido, 1, documentos[1]), comFilhos,
            Folha(Guid.CreateVersion7(), 4, documentos[4], quantidadeMinima: null), FolhaSemDocumentoAssociado(0), .. comFilhos.Filhos,
        ];

        Func<string[]> validar = () => CodigosDe(fase, documentos, nos);

        validar.Should().NotThrow().Which
            .Should().Equal(IdDuplicado, OrdemDeRaizDuplicada, FolhaComFilhos, QuantidadeMinimaAusente, FolhaSemDocumento);
    }

    [Fact(DisplayName = "O Id de nó repetido é recusado sem reconstruir os nós repetidos")]
    public void IdDuplicado_Isolado()
    {
        FaseCronograma fase = FaseConforme();
        DocumentoExigido[] documentos = Documentos(fase, 2);
        Guid repetido = Guid.CreateVersion7();

        CodigosDe(fase, documentos, [Folha(repetido, 0, documentos[0]), Folha(repetido, 1, documentos[1])]).Should().Equal(IdDuplicado);
    }

    [Fact(DisplayName = "A folha com filhos é recusada sem reconstruir a folha")]
    public void FolhaComFilhos_Isolada()
    {
        FaseCronograma fase = FaseConforme();
        DocumentoExigido[] documentos = Documentos(fase, 2);
        NoExigencia filho = Folha(Guid.CreateVersion7(), 0, documentos[1]);

        CodigosDe(fase, documentos, [ComFilhos(0, documentos[0], filho), filho]).Should().Equal(FolhaComFilhos);
    }

    [Fact(DisplayName = "A folha sem documento associado é recusada, sem lançar, e as demais raízes seguem conferidas")]
    public void FolhaSemDocumento_Isolada()
    {
        FaseCronograma fase = FaseConforme();
        DocumentoExigido[] documentos = Documentos(fase, 1);

        CodigosDe(fase, documentos, [Folha(Guid.CreateVersion7(), 0, documentos[0]), FolhaSemDocumentoAssociado(1)]).Should().Equal(FolhaSemDocumento);
    }

    private static string[] CodigosDe(FaseCronograma fase, DocumentoExigido[] documentos, NoExigencia[] nos) =>
        [.. ValidacaoDaArvoreRestaurada.Validar(GrafoComArvore(fase, documentos, nos)).Select(static causa => causa.Error.Code)];

    private static DocumentoExigido[] Documentos(FaseCronograma fase, int quantidade) => [.. Enumerable.Range(0, quantidade).Select(_ => DocumentoQualquer(fase.Id))];

    private static NoExigencia Folha(Guid id, int ordem, DocumentoExigido documento, int? quantidadeMinima = 1) =>
        NoExigencia.Reidratar(id, TipoNo.Folha, ordem, documento.Id, documento, quantidadeMinima, null, null, null, null, null, [], []);

    private static NoExigencia ComFilhos(int ordem, DocumentoExigido documento, params NoExigencia[] filhos) =>
        NoExigencia.Reidratar(Guid.CreateVersion7(), TipoNo.Folha, ordem, documento.Id, documento, 1, null, null, null, null, null, [], filhos);

    private static NoExigencia FolhaSemDocumentoAssociado(int ordem) =>
        NoExigencia.Reidratar(Guid.CreateVersion7(), TipoNo.Folha, ordem, null, null, 1, null, null, null, null, null, [], []);
}
