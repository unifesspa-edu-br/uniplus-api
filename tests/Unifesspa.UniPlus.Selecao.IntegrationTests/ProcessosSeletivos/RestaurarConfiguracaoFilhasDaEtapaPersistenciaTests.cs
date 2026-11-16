namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// O descarte da retificação contra <b>Postgres real</b>: a etapa retida volta com as filhas que o ato
/// congelou (janela, produtos, bancas e recursos).
/// </summary>
public sealed class RestaurarConfiguracaoFilhasDaEtapaPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact(DisplayName = "O descarte devolve à etapa a janela, os produtos, as bancas e os recursos que o ato congelou")]
    public async Task Restaurar_ReponAsFilhasDaEtapaRetida()
    {
        // Variante própria: a classe inteira compartilha um Postgres, e o id da etapa é fixo
        // por variante — dois processos com as mesmas etapas colidiriam na chave primária.
        const int Variante = 3;
        ProcessoSeletivo original = CorpusEnvelopeProcessoRico.ProcessoRico(Variante);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(original));
        CorpusEnvelopeEntrada.Publicar(original);

        Guid processoId = original.Id;
        Guid objetivaId = original.Etapas.Single(e => e.Nome == "Prova Objetiva").Id;
        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(
            original, congelado.Bytes, new Guid($"01900000-0000-7000-8000-00000000000{Variante:x}"));

        await using (UniPlusDbContext escrita = fixture.CreateDbContext())
        {
            escrita.ProcessosSeletivos.Add(original);
            escrita.Add(versao);
            await escrita.SaveChangesAsync();
        }

        // A sessão editorial esvazia a etapa RETIDA pelos comandos que o operador tem à mão —
        // não por um grafo. É a diferença que faz o teste dizer algo: aplicar um grafo cuja
        // etapa nasce vazia passaria por inércia se a reposição não mexesse nas coleções, já
        // que elas nunca teriam saído.
        await using (UniPlusDbContext sessao = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await RestaurarConfiguracaoPersistenciaTests.CarregarAsync(sessao, processoId);
            EtapaProcesso objetiva = tracked.Etapas.Single(e => e.Id == objetivaId);

            // Recursos antes dos produtos: a âncora em ato é conferida contra os produtos da
            // etapa, e esvaziá-los primeiro deixaria a janela recursal sem onde ancorar.
            objetiva.DefinirRecursos([]).IsSuccess.Should().BeTrue();
            objetiva.DefinirProdutos([]).IsSuccess.Should().BeTrue();
            objetiva.DefinirBancas([]).IsSuccess.Should().BeTrue();
            objetiva.DefinirJanelaEParecer(
                new DateTimeOffset(2027, 5, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 5, 2, 0, 0, 0, TimeSpan.Zero),
                emiteParecerIndividual: false).IsSuccess.Should().BeTrue();

            // Troca o rótulo de um item, mantendo o id, e acrescenta um item novo no lugar do outro.
            ItemDeAvaliacao matematica = objetiva.ItensDeAvaliacao.Single(i => i.Rotulo == "Bloco de Matemática");
            objetiva.AtualizarDados(
                new IdentificacaoDaEtapa(Nome: objetiva.Nome, Carater: objetiva.Carater, TipoEtapa: objetiva.TipoEtapa, Ordem: objetiva.Ordem, FaseCodigo: objetiva.FaseCodigo),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: objetiva.Peso, NotaMinima: objetiva.NotaMinima, NotaMaxima: objetiva.NotaMaxima, PassoDoLancamento: objetiva.PassoDoLancamento, FormaDeAvaliacao: objetiva.FormaDeAvaliacao, RegraConsolidacao: objetiva.RegraConsolidacao, EscalaDeAtendimento: null),
                [new(matematica.Id, "Bloco de Exatas", 40m), new(null, "Bloco de Humanidades", 60m)]).IsSuccess.Should().BeTrue();

            await sessao.SaveChangesAsync();
        }

        await using (UniPlusDbContext leitura = fixture.CreateDbContext())
        {
            EtapaProcesso suja = (await RestaurarConfiguracaoPersistenciaTests.CarregarAsync(leitura, processoId)).Etapas.Single(e => e.Id == objetivaId);
            suja.ItensDeAvaliacao.Select(i => i.Rotulo).Should().BeEquivalentTo("Bloco de Exatas", "Bloco de Humanidades");
            suja.Produtos.Should().BeEmpty("pré-condição: a sessão editorial esvaziou a etapa");
            suja.Recursos.Should().BeEmpty("pré-condição: a sessão editorial esvaziou a etapa");
            suja.EmiteParecerIndividual.Should().BeFalse("pré-condição: a sessão editorial retirou a promessa de parecer");
        }

        await using (UniPlusDbContext descarte = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await RestaurarConfiguracaoPersistenciaTests.CarregarAsync(descarte, processoId);

            Result<GrafoConfiguracao> prova = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(tracked, versao);
            prova.IsSuccess.Should().BeTrue(prova.Error?.Message);

            tracked.LimparColetaEDerivacaoParaRestauracao();
            await descarte.SaveChangesAsync();

            tracked.RestaurarConfiguracaoCongelada(versao, prova.Value!).IsSuccess.Should().BeTrue();
            await descarte.SaveChangesAsync();
        }

        await using UniPlusDbContext verificacao = fixture.CreateDbContext();
        ProcessoSeletivo reposto = await RestaurarConfiguracaoPersistenciaTests.CarregarAsync(verificacao, processoId);
        EtapaProcesso objetivaReposta = reposto.Etapas.Single(e => e.Id == objetivaId);
        EtapaProcesso congeladaOriginal = CorpusEnvelopeProcessoRico.ProcessoRico(Variante).Etapas.Single(e => e.Id == objetivaId);

        // O item volta pelo id que o envelope congela: lançamento e recurso apontam para ele, e a
        // linha da sessão editorial que trocou de rótulo é a mesma que recupera o rótulo original.
        objetivaReposta.ItensDeAvaliacao.Select(i => (i.Id, i.Rotulo, i.PontuacaoMaxima))
            .Should().BeEquivalentTo(congeladaOriginal.ItensDeAvaliacao.Select(i => (i.Id, i.Rotulo, i.PontuacaoMaxima)));
        objetivaReposta.Inicio.Should().Be(congeladaOriginal.Inicio);
        objetivaReposta.Fim.Should().Be(congeladaOriginal.Fim);
        objetivaReposta.EmiteParecerIndividual.Should().Be(congeladaOriginal.EmiteParecerIndividual);
        // Só o lado esperado normalizado, pela mesma razão da outra reposição: o que volta tem
        // de ser o texto canônico, e afrouxar os dois lados esconderia o dia em que deixar de
        // ser.
        objetivaReposta.Produtos.Select(p => (p.Id, p.AtoCodigo, p.Papel))
            .Should().BeEquivalentTo(congeladaOriginal.Produtos.Select(p => (p.Id, HashCanonicalComputer.NormalizeNfc(p.AtoCodigo), p.Papel)));
        // A banca volta pelo CONTEÚDO, não pela identidade: o envelope congela o tipo e o
        // código, e não o id — que a etapa refaz a cada gravação, porque o comando declara a
        // banca pelo tipo. Cobrar o id aqui seria cobrar o que a publicação não promete.
        objetivaReposta.Bancas.Select(b => (b.TipoBancaOrigemId, b.Codigo))
            .Should().BeEquivalentTo(congeladaOriginal.Bancas.Select(b => (b.TipoBancaOrigemId, HashCanonicalComputer.NormalizeNfc(b.Codigo))));
        // Como nas bancas, pelo CONTEÚDO: o envelope congela a janela — âncora, regra e prazos —,
        // não a linha que a guarda. O produto que a âncora aponta continua sendo cobrado, porque
        // esse id o envelope congela de fato.
        objetivaReposta.Recursos.Select(r => (r.Ancora, r.Regra.Codigo, r.Args.PrazoValor, r.ProdutoAncoraId))
            .Should().BeEquivalentTo(congeladaOriginal.Recursos
                .Select(r => (r.Ancora, r.Regra.Codigo, r.Args.PrazoValor, r.ProdutoAncoraId)));

        // A prova que fecha: o agregado relido recanonicaliza nos bytes que o ato congelou. Sem
        // ela, repor "quase tudo" passaria — e o descarte relataria sucesso com a configuração
        // viva ainda diferente da publicação.
        Result<SnapshotCanonico> recodificado = CorpusEnvelope.Registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(
                reposto, CorpusEnvelopeDadosDoEdital.DadosRicos(), CorpusEnvelope.HashDocumento, FusoInstitucional.ZoneId,
                ValoresSelecionaveisCongelados: CorpusEnvelopeDadosDoEdital.ValoresSelecionaveisRicos(),
                CalendarioDiasUteis: CorpusEnvelopeDadosDoEdital.CalendarioRico(),
                AgregadosDosGrupos: CorpusEnvelopeEntrada.AgregadosRicos,
                ApresentacaoDosPressupostos: CorpusEnvelopeEntrada.ApresentacaoRica,
                TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(reposto), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));

        recodificado.Value!.Bytes.Should().Equal(congelado.Bytes,
            "a etapa que o descarte devolve tem de ser a que o documento publicado descreve — janela, produtos, " +
            "bancas e janelas recursais inclusive");
    }
}
