namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

internal static class ApoioDoRestauradorDeConfiguracao
{
    internal static VersaoConfiguracao PublicadaPeloAtorDoCorpus(
        ProcessoSeletivo processo, DadosEdital dados, SnapshotCanonico congelado)
    {
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, CorpusEnvelope.HashDocumento), AtorUsuarioContaId = CorpusEnvelope.Ator, Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico() });
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
        return publicacao.Value!;
    }

    // A APLICAÇÃO na raiz viva é do descarte (Story #986): limpa as coleções mutáveis e repõe o
    // grafo provado.
    internal static void ReporORestauradoNaRaizViva(ProcessoSeletivo processo, VersaoConfiguracao versao)
    {
        Result<GrafoConfiguracao> resultado = new RestauradorDeConfiguracao(new RegistroCodecsEnvelope()).Restaurar(processo, versao);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.LimparColetaEDerivacaoParaRestauracao();
        processo.RestaurarConfiguracaoCongelada(versao, resultado.Value!).IsSuccess.Should().BeTrue();
    }



    internal static EnvelopeReidratado Reidratado(VersaoConfiguracao versao)
    {
        Result<EnvelopeReidratado> reidratado = CorpusEnvelope.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);
        return reidratado.Value!;
    }

    internal static GrafoConfiguracao RestauradoSemErro(ProcessoSeletivo processo, VersaoConfiguracao versao, string porque)
    {
        Result<GrafoConfiguracao> resultado = new RestauradorDeConfiguracao(new RegistroCodecsEnvelope()).Restaurar(processo, versao);
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message ?? porque);
        return resultado.Value!;
    }
}
