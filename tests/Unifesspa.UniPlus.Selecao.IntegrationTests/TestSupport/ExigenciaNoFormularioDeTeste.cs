namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>A exigência de documento geral e obrigatória que um formulário do processo entrega, na fase dele.</summary>
internal static class ExigenciaNoFormularioDeTeste
{
    public static void Exigir(ProcessoSeletivo processo, string formularioCodigo, Guid faseId)
    {
        ArgumentNullException.ThrowIfNull(processo);

        DocumentoExigido exigencia = DocumentoExigido.Criar(
            ChaveLocal.Criar("exigencia_comprovante_de_residencia").Value!,
            new DocumentoExigidoDeclarado(
                new CopiaDoTipoDeDocumento(TipoDocumentoSeed.Itens[0].Id, "COMPROVANTE_DE_RESIDENCIA", "Comprovante de residência", "IDENTIFICACAO", ProtecaoDoDocumentoDeTeste.Pessoal),
                new MomentoDaExigencia(faseId, null, FormulariosDoProcessoDeTeste.Codigo(formularioCodigo)),
                new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
                new IndeferimentoDoDocumento(true, null, [BaseLegalDaExigencia()]),
                RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
                null)).Value!;
        Result definida = processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(exigencia, 0).Value!]);
        definida.IsSuccess.Should().BeTrue(definida.Error?.Message);
    }

    private static DocumentoExigidoBaseLegal BaseLegalDaExigencia() => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
}
