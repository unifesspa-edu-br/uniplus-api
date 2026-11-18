namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A exigência com identidade nova usa a proteção atual do tipo, mesmo que outra exigência desse tipo já tenha proteção congelada.</summary>
public sealed class ProtecaoDaExigenciaNovaNaRetificacaoTests
{
    [Fact(DisplayName = "A exigência nova do mesmo tipo reclassificado usa a proteção pessoal atual, sem herdar a sensível da exigência anterior")]
    public void Montar_ExigenciaNovaDoMesmoTipo_UsaProtecaoAtual()
    {
        ProcessoSeletivo processo = FatoCitadoDeTeste.ProcessoQueColetaOFato(comItemNovo: false);
        DocumentoExigido nova = processo.DocumentosExigidos.Single();
        TipoDocumentoView tipoAtual = TipoPessoal(nova);
        CongeladoNaVersaoVigente anterior = VersaoComOutraExigenciaDoTipo(nova.TipoDocumentoOrigemId);

        ProtecaoVigenteDoProcesso protecao = ProtecaoVigenteDasExigenciasDoProcesso.Montar(
            processo, new Dictionary<Guid, TipoDocumentoView> { [tipoAtual.Id] = tipoAtual }, [], anterior);

        anterior.Exigencias.Should().NotContainKey(nova.Id);
        anterior.Exigencias.Values.Single().Protecao.Classificacao.Should().Be(ClassificacaoDeProtecao.Sensivel);
        protecao.Exigencias.PorExigencia[nova.Id].Should().Be(new ProtecaoDoDocumento(ClassificacaoDeProtecao.Pessoal, "Finalidade atual.", "CUMPRIMENTO_OBRIGACAO_LEGAL"));
    }

    private static TipoDocumentoView TipoPessoal(DocumentoExigido exigencia) => new(
        exigencia.TipoDocumentoOrigemId, exigencia.TipoDocumentoCodigo, exigencia.TipoDocumentoNome, exigencia.TipoDocumentoCategoria,
        ClassificacaoDeProtecao.Pessoal, "Finalidade atual.", "CUMPRIMENTO_OBRIGACAO_LEGAL", ProtecaoAprovada: true);

    private static CongeladoNaVersaoVigente VersaoComOutraExigenciaDoTipo(Guid tipoId) => new(
        new Dictionary<Guid, ExigenciaCongelada>
        {
            [Guid.CreateVersion7()] = new(tipoId, ProtecaoDoDocumentoDeTeste.De(ClassificacaoDeProtecao.Sensivel)),
        },
        new Dictionary<string, FatoDerivadoCongelado>());
}
