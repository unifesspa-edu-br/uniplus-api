namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Xunit;

/// <summary>
/// O identificador legível na sessão de retificação, sobre Postgres real e com o envelope de
/// verdade: o que a versão vigente congelou é o que decide se a sessão pode declará-lo.
/// </summary>
/// <remarks>
/// O ponto de partida é um certame publicado cuja versão congelou <b>sem</b> identificador — o
/// estado de um processo publicado antes de o campo existir. A publicação de hoje o exige, então
/// o estado é montado como aquele processo o teria: o envelope congelado sem o valor e a raiz viva
/// também sem ele. A versão é append-only no banco, e por isso nasce já com esses bytes.
/// </remarks>
public sealed class IdentificadorLegivelNaRetificacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact(DisplayName = "Descartar a sessão repõe a ausência congelada, e não o identificador declarado nela")]
    public async Task Descarte_RepoeAusenciaCongelada()
    {
        (Guid processoId, VersaoConfiguracao versao) = await SemearPublicadoSemIdentificadorAsync(variante: 4);
        await DeclararNaSessaoAsync(processoId, versao, Declarado(4));

        await using (SelecaoDbContext descarte = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await RestaurarConfiguracaoPersistenciaTests.CarregarAsync(descarte, processoId);
            Result<GrafoConfiguracao> prova = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(tracked, versao);
            prova.IsSuccess.Should().BeTrue(prova.Error?.Message);

            tracked.LimparColetaEDerivacaoParaRestauracao();
            await descarte.SaveChangesAsync();
            tracked.RestaurarConfiguracaoCongelada(versao, prova.Value!).IsSuccess.Should().BeTrue();
            Result descartado = tracked.DescartarRetificacao(PrecondicaoIfMatch.DeTags([tracked.ETagDaSessaoEditorial!]));
            descartado.IsSuccess.Should().BeTrue(descartado.Error?.Message);
            await descarte.SaveChangesAsync();
        }

        await using SelecaoDbContext leitura = fixture.CreateDbContext();
        ProcessoSeletivo reposto = await RestaurarConfiguracaoPersistenciaTests.CarregarAsync(leitura, processoId);
        reposto.IdentificadorLegivel.Should().BeNull(
            "o descarte devolve o que a versão vigente congelou — a declaração da sessão não sobrevive");
        reposto.Rascunho.Should().BeNull();
    }

    [Fact(DisplayName = "O fechamento congela o identificador declarado, e a sessão seguinte não o troca mais")]
    public async Task Fechamento_CongelaEFechaAPorta()
    {
        (Guid processoId, VersaoConfiguracao versao) = await SemearPublicadoSemIdentificadorAsync(variante: 5);
        IdentificadorLegivel declarado = Declarado(5);
        await DeclararNaSessaoAsync(processoId, versao, declarado);

        VersaoConfiguracao nova;
        await using (SelecaoDbContext fechamento = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await RestaurarConfiguracaoPersistenciaTests.CarregarAsync(fechamento, processoId);
            SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(
                tracked, new RetificacaoInfo(versao.AtoCriadorId, tracked.Rascunho!.Motivo)));

            Result<VersaoConfiguracao> fechar = tracked.FecharRetificacao(
                CorpusEnvelope.DadosRicos(), versao, congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash,
                CorpusEnvelope.HashDocumento, CorpusEnvelope.Ator, PrecondicaoIfMatch.Curinga, TimeProvider.System,
                CorpusEnvelope.ContextoRico());
            fechar.IsSuccess.Should().BeTrue(fechar.Error?.Message);
            nova = fechar.Value!;
            fechamento.Add(nova);
            await fechamento.SaveChangesAsync();
        }

        Result<EnvelopeReidratado> reidratada = CorpusEnvelope.Registro.Reidratar(nova);
        reidratada.IsSuccess.Should().BeTrue(reidratada.Error?.Message);
        reidratada.Value!.Grafo.IdentificadorLegivel.Should().Be(
            declarado, "o identificador declarado na sessão é congelado pela versão que ela gera");

        await using SelecaoDbContext novaSessao = fixture.CreateDbContext();
        ProcessoSeletivo processo = await RestaurarConfiguracaoPersistenciaTests.CarregarAsync(novaSessao, processoId);
        processo.AbrirRetificacao(
                "Tenta trocar o endereço", nova, reidratada.Value.Grafo.IdentificadorLegivel, CorpusEnvelope.Ator,
                DateTimeOffset.UtcNow)
            .IsSuccess.Should().BeTrue();

        Result troca = processo.DefinirIdentificadorLegivel(
            IdentificadorLegivel.Criar("medicina-2028-v5").Value, PrecondicaoIfMatch.DeTags([processo.ETagDaSessaoEditorial!]));

        troca.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentificadorLegivelImutavel,
            "agora o identificador consta em versão publicada");
    }

    /// <summary>Valor distinto por variante: os testes da classe compartilham o mesmo banco.</summary>
    private static IdentificadorLegivel Declarado(int variante) =>
        IdentificadorLegivel.Criar($"medicina-2027-v{variante}").Value;

    /// <summary>
    /// Publica um processo do corpus cuja versão congelou sem identificador, com a raiz viva também
    /// sem ele, e persiste os dois.
    /// </summary>
    private async Task<(Guid ProcessoId, VersaoConfiguracao Versao)> SemearPublicadoSemIdentificadorAsync(int variante)
    {
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico(variante);
        IdentificadorLegivel? original = processo.IdentificadorLegivel;
        processo.DefinirIdentificadorLegivel(null, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        SnapshotCanonico semIdentificador = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(processo));

        // A publicação de hoje exige o identificador: publica com ele e o retira em seguida, como
        // estaria a raiz de um processo publicado antes de o campo existir.
        processo.DefinirIdentificadorLegivel(original, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        CorpusEnvelope.Publicar(processo);
        typeof(ProcessoSeletivo).GetProperty(nameof(ProcessoSeletivo.IdentificadorLegivel))!.SetValue(processo, null);

        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(
            processo, semIdentificador.Bytes, new Guid($"01900000-0000-7000-8000-0000000014{variante:x2}"));

        await using SelecaoDbContext escrita = fixture.CreateDbContext();
        escrita.ProcessosSeletivos.Add(processo);
        escrita.Add(versao);
        await escrita.SaveChangesAsync();
        return (processo.Id, versao);
    }

    /// <summary>
    /// Abre a sessão como o handler abre — com o identificador lido do envelope da versão base — e
    /// declara o identificador nela.
    /// </summary>
    private async Task DeclararNaSessaoAsync(Guid processoId, VersaoConfiguracao versao, IdentificadorLegivel declarado)
    {
        Result<EnvelopeReidratado> baseReidratada = CorpusEnvelope.Registro.Reidratar(versao);
        baseReidratada.IsSuccess.Should().BeTrue(baseReidratada.Error?.Message);
        baseReidratada.Value!.Grafo.IdentificadorLegivel.Should().BeNull("pré-condição: a versão congelou sem identificador");

        await using SelecaoDbContext sessao = fixture.CreateDbContext();
        ProcessoSeletivo tracked = await RestaurarConfiguracaoPersistenciaTests.CarregarAsync(sessao, processoId);
        tracked.AbrirRetificacao(
                "Declara o endereço público", versao, baseReidratada.Value.Grafo.IdentificadorLegivel,
                CorpusEnvelope.Ator, DateTimeOffset.UtcNow)
            .IsSuccess.Should().BeTrue();
        tracked.DefinirIdentificadorLegivel(declarado, PrecondicaoIfMatch.DeTags([tracked.ETagDaSessaoEditorial!]))
            .IsSuccess.Should().BeTrue("a versão base não congelou identificador — a sessão o declara");
        await sessao.SaveChangesAsync();
    }
}
