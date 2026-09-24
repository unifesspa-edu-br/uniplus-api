namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Xunit;

/// <summary>
/// A reposição contra <b>Postgres real</b> — o risco que o round-trip puro <b>não
/// enxerga</b> (Story #859; ADR-0110 D2).
/// </summary>
/// <remarks>
/// <para>
/// O round-trip byte-a-byte é cego para tudo o que o envelope não serializa: as
/// <b>chaves estrangeiras internas</b>, o <c>EntityState</c>, o <b>cascade</b> das filhas
/// substituídas, o <c>CreatedAt</c>, e — o pior — a colisão do <b>identity map</b> do EF
/// quando uma instância nova carrega o <c>Id</c> de uma entidade já <i>tracked</i>. Ele
/// passaria <b>mesmo se <c>VincularProcesso</c> nunca fosse chamado</b>.
/// </para>
/// <para>
/// Este teste fecha essa cegueira: carrega o agregado <i>tracked</i> pelo grafo completo,
/// repõe, <c>SaveChanges</c>, <b>limpa o tracker</b> e recarrega do banco. O que
/// sobrevive à ida e à volta é o que de fato foi persistido.
/// </para>
/// </remarks>
public sealed class RestaurarConfiguracaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact(DisplayName = "A reposição sobrevive ao banco: FKs, cascade, identity map e o Id congelado da etapa")]
    public async Task Restaurar_PersisteOGrafoInteiro()
    {
        // Variante própria: os dois testes desta classe compartilham o mesmo Postgres, e o
        // etapa.Id é FIXO no corpus (é o que torna a golden fixture determinística). Dois
        // processos com as mesmas etapas colidiriam na chave primária de etapas_processo.
        const int Variante = 1;
        ProcessoSeletivo original = CorpusEnvelope.ProcessoRico(Variante);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(original));
        CorpusEnvelope.Publicar(original);

        Guid processoId = original.Id;
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(
            original, congelado.Bytes, new Guid($"01900000-0000-7000-8000-00000000000{Variante:x}"));

        await using (SelecaoDbContext escrita = fixture.CreateDbContext())
        {
            escrita.ProcessosSeletivos.Add(original);
            escrita.Add(versao);
            await escrita.SaveChangesAsync();
        }

        // A "sessão editorial": a configuração viva é substituída por outra, e PERSISTIDA.
        // É o estado real de que o descarte parte — não um agregado em memória.
        await using (SelecaoDbContext sessao = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(sessao, processoId);
            tracked.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoPobre(Variante)).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync();
        }

        await using (SelecaoDbContext leitura = fixture.CreateDbContext())
        {
            ProcessoSeletivo sujo = await CarregarAsync(leitura, processoId);
            sujo.Etapas.Should().ContainSingle("pré-condição: a sessão editorial trocou as 3 etapas por 1");
            sujo.BonusRegional.Should().BeNull("pré-condição: a sessão editorial removeu o bônus");
        }

        // O DESCARTE — com a prova de round-trip, exatamente como em produção.
        await using (SelecaoDbContext descarte = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(descarte, processoId);

            // A prova é do restaurador; a APLICAÇÃO com flush intermediário é do descarte (Story
            // #986): limpa as coleções mutáveis, flusha os DELETEs, e só então repõe o grafo
            // congelado (INSERT) — os DELETEs saem antes dos INSERTs, sem colidir no índice único.
            Result<GrafoConfiguracao> prova = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(tracked, versao);
            prova.IsSuccess.Should().BeTrue(prova.Error?.Message);

            tracked.LimparColetaEDerivacaoParaRestauracao();
            await descarte.SaveChangesAsync();

            tracked.RestaurarConfiguracaoCongelada(versao, prova.Value!).IsSuccess.Should().BeTrue();
            await descarte.SaveChangesAsync();
        }

        // A verificação: tracker LIMPO, tudo relido do banco.
        await using SelecaoDbContext verificacao = fixture.CreateDbContext();
        ProcessoSeletivo reposto = await CarregarAsync(verificacao, processoId);

        // (a) As FKs foram reconstruídas — o encoder não as serializa, então o round-trip
        //     passaria mesmo se VincularProcesso nunca tivesse sido chamado.
        reposto.Etapas.Should().OnlyContain(e => e.ProcessoSeletivoId == processoId);
        reposto.DistribuicaoVagas.Should().OnlyContain(d => d.ProcessoSeletivoId == processoId);
        reposto.CriteriosDesempate.Should().OnlyContain(c => c.ProcessoSeletivoId == processoId);
        reposto.OfertaAtendimento!.ProcessoSeletivoId.Should().Be(processoId);
        reposto.BonusRegional!.ProcessoSeletivoId.Should().Be(processoId);
        reposto.Classificacao!.ProcessoSeletivoId.Should().Be(processoId);

        foreach (ConfiguracaoDistribuicaoVagas distribuicao in reposto.DistribuicaoVagas)
        {
            distribuicao.Modalidades.Should()
                .OnlyContain(m => m.ConfiguracaoDistribuicaoVagasId == distribuicao.Id);
        }

        reposto.Classificacao.RegrasEliminacao.Should()
            .OnlyContain(r => r.ConfiguracaoClassificacaoId == reposto.Classificacao.Id);
        reposto.OfertaAtendimento.Condicoes.Should()
            .OnlyContain(c => c.OfertaAtendimentoEspecializadoId == reposto.OfertaAtendimento.Id);

        // (b) As filhas da sessão editorial saíram — sem órfãs.
        reposto.Etapas.Should().HaveCount(3);
        reposto.DistribuicaoVagas.Should().HaveCount(2);
        reposto.CriteriosDesempate.Should().HaveCount(5);
        reposto.Classificacao.RegrasEliminacao.Should().HaveCount(4);
        reposto.OfertaAtendimento.Condicoes.Should().HaveCount(2);
        reposto.OfertaAtendimento.TiposDeficiencia.Should().HaveCount(2);
        reposto.BonusRegional.Fator.Should().Be(1.2000m, "o bônus voltou — com o fator congelado");

        // (c) O Id congelado da etapa sobreviveu ao banco — é ele que o etapaRef referencia.
        IEnumerable<Guid> idsCongelados = CorpusEnvelope.ProcessoRico(Variante).Etapas.Select(e => e.Id);
        reposto.Etapas.Select(e => e.Id).Should().BeEquivalentTo(idsCongelados);

        // (d) E o agregado RELIDO DO BANCO recanonicaliza nos bytes congelados. É a prova
        //     de que nada se perdeu no caminho de ida e volta pela persistência — nem um
        //     decimal arredondado pela coluna, nem um enum, nem a ordem de um array.
        Result<SnapshotCanonico> recodificado = CorpusEnvelope.Registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(
                reposto, CorpusEnvelope.DadosRicos(), CorpusEnvelope.HashDocumento, FusoInstitucional.ZoneId,
                ValoresSelecionaveisCongelados: CorpusEnvelope.ValoresSelecionaveisRicos(),
                CalendarioDiasUteis: CorpusEnvelope.CalendarioRico()));

        recodificado.Value!.Bytes.Should().Equal(congelado.Bytes,
            "o agregado que voltou do Postgres tem de recanonicalizar nos MESMOS bytes que o ato congelou — é a " +
            "única prova de que o descarte devolveu o certame ao que o documento publicado diz");
    }

    [Fact(DisplayName = "A etapa que sobrevive ao descarte preserva o CreatedAt original (D2)")]
    public async Task EtapaReconciliada_PreservaCreatedAt()
    {
        const int Variante = 2;
        ProcessoSeletivo original = CorpusEnvelope.ProcessoRico(Variante);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(original));
        CorpusEnvelope.Publicar(original);

        Guid processoId = original.Id;
        Guid etapaId = original.Etapas.First().Id;
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(
            original, congelado.Bytes, new Guid($"01900000-0000-7000-8000-00000000000{Variante:x}"));

        await using (SelecaoDbContext escrita = fixture.CreateDbContext())
        {
            escrita.ProcessosSeletivos.Add(original);
            escrita.Add(versao);
            await escrita.SaveChangesAsync();
        }

        DateTimeOffset criadoEm;
        await using (SelecaoDbContext leitura = fixture.CreateDbContext())
        {
            criadoEm = (await CarregarAsync(leitura, processoId)).Etapas.Single(e => e.Id == etapaId).CreatedAt;
        }

        criadoEm.Should().NotBe(default, "pré-condição: o AuditableInterceptor carimbou o CreatedAt no INSERT");

        // A SESSÃO EDITORIAL altera os DADOS da etapa, PRESERVANDO o Id — é o cenário que o
        // descarte tem de desfazer, e é o único que testa a reconciliação de verdade.
        // Restaurar sobre uma configuração já idêntica passaria até com uma implementação
        // que não fizesse NADA: o CreatedAt estaria preservado por inércia, e os dados
        // "voltariam" porque nunca saíram.
        await using (SelecaoDbContext sessao = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(sessao, processoId);
            tracked.RestaurarConfiguracaoCongelada(versao, CorpusEnvelope.GrafoComEtapaAlterada(Variante))
                .IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync();
        }

        await using (SelecaoDbContext conferencia = fixture.CreateDbContext())
        {
            EtapaProcesso alterada = (await CarregarAsync(conferencia, processoId)).Etapas.Single(e => e.Id == etapaId);
            alterada.Nome.Should().Be("Etapa Descaracterizada", "pré-condição: a sessão editorial mudou os dados");
            alterada.CreatedAt.Should().Be(criadoEm, "pré-condição: a linha é a MESMA — só os dados mudaram");
        }

        // O DESCARTE — reconcilia por Id na instância tracked. Substituí-la por uma instância
        // nova com o mesmo Id colidiria com o identity map, e o CreatedAt seria recarimbado.
        await using (SelecaoDbContext descarte = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(descarte, processoId);

            // Orquestração do descarte (Story #986): prova, limpa fatos/regras + flush, aplica. A
            // etapa NÃO é limpa — reconcilia por Id (reuse-tracked), preservando o CreatedAt.
            Result<GrafoConfiguracao> prova = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(tracked, versao);
            prova.IsSuccess.Should().BeTrue(prova.Error?.Message);

            tracked.LimparColetaEDerivacaoParaRestauracao();
            await descarte.SaveChangesAsync();

            tracked.RestaurarConfiguracaoCongelada(versao, prova.Value!).IsSuccess.Should().BeTrue();
            await descarte.SaveChangesAsync();
        }

        await using SelecaoDbContext verificacao = fixture.CreateDbContext();
        EtapaProcesso reconciliada = (await CarregarAsync(verificacao, processoId)).Etapas.Single(e => e.Id == etapaId);
        EtapaProcesso congeladaOriginal = CorpusEnvelope.ProcessoRico(Variante).Etapas.Single(e => e.Id == etapaId);

        // Todos os dados voltaram ao que a versão congelou...
        reconciliada.Nome.Should().Be(congeladaOriginal.Nome);
        reconciliada.Carater.Should().Be(congeladaOriginal.Carater);
        reconciliada.Peso.Should().Be(congeladaOriginal.Peso);
        reconciliada.NotaMinima.Should().Be(congeladaOriginal.NotaMinima);
        reconciliada.Ordem.Should().Be(congeladaOriginal.Ordem);

        // ...inclusive o que não é escalar. A etapa descaracterizada pela sessão perdeu a
        // janela própria, a promessa de parecer, os produtos, as bancas e as janelas recursais;
        // repor só os escalares deixaria o descarte relatar sucesso com a configuração viva
        // ainda diferente do envelope publicado, que é precisamente o que ele desfaz.
        reconciliada.Inicio.Should().Be(congeladaOriginal.Inicio);
        reconciliada.Fim.Should().Be(congeladaOriginal.Fim);
        reconciliada.EmiteParecerIndividual.Should().Be(congeladaOriginal.EmiteParecerIndividual);

        // Só o lado ESPERADO é normalizado. O operador pode declarar o acento em forma
        // decomposta, e é a composta que atravessa o congelamento: o que volta do descarte é o
        // texto canônico, não o que foi digitado. Normalizar também o lado restaurado faria a
        // asserção passar se o decodificador, a reposição ou a persistência passassem a
        // devolver texto decomposto — que é precisamente a regressão que interessa pegar aqui,
        // e que a recanonicalização adiante também não veria.
        reconciliada.Produtos.Select(p => (p.AtoCodigo, p.Papel))
            .Should().BeEquivalentTo(congeladaOriginal.Produtos.Select(p => (HashCanonicalComputer.NormalizeNfc(p.AtoCodigo), p.Papel)));
        reconciliada.Bancas.Select(b => (b.Codigo, b.TipoBancaOrigemId))
            .Should().BeEquivalentTo(congeladaOriginal.Bancas.Select(b => (HashCanonicalComputer.NormalizeNfc(b.Codigo), b.TipoBancaOrigemId)));
        reconciliada.Recursos.Select(r => (r.Ancora, r.Regra.Codigo, r.Args.PrazoValor, r.ProdutoAncoraId))
            .Should().BeEquivalentTo(congeladaOriginal.Recursos
                .Select(r => (r.Ancora, r.Regra.Codigo, r.Args.PrazoValor, r.ProdutoAncoraId)));

        // ...e o CreatedAt não voltou, porque nunca saiu: é a MESMA linha (ADR-0110 D2).
        reconciliada.CreatedAt.Should().Be(criadoEm,
            "a etapa reconciliada é a mesma linha — a D2 declara que ela preserva o CreatedAt original, ao contrário " +
            "das demais filhas, que são recriadas e recebem o instante do descarte");
    }

    [Fact(DisplayName = "O descarte devolve à etapa a janela, os produtos, as bancas e os recursos que o ato congelou")]
    public async Task Restaurar_ReponAsFilhasDaEtapaRetida()
    {
        // Variante própria: a classe inteira compartilha um Postgres, e o id da etapa é fixo
        // por variante — dois processos com as mesmas etapas colidiriam na chave primária.
        const int Variante = 3;
        ProcessoSeletivo original = CorpusEnvelope.ProcessoRico(Variante);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelope.Entrada(original));
        CorpusEnvelope.Publicar(original);

        Guid processoId = original.Id;
        Guid objetivaId = original.Etapas.Single(e => e.Nome == "Prova Objetiva").Id;
        VersaoConfiguracao versao = CorpusEnvelope.VersaoDeAbertura(
            original, congelado.Bytes, new Guid($"01900000-0000-7000-8000-00000000000{Variante:x}"));

        await using (SelecaoDbContext escrita = fixture.CreateDbContext())
        {
            escrita.ProcessosSeletivos.Add(original);
            escrita.Add(versao);
            await escrita.SaveChangesAsync();
        }

        // A sessão editorial esvazia a etapa RETIDA pelos comandos que o operador tem à mão —
        // não por um grafo. É a diferença que faz o teste dizer algo: aplicar um grafo cuja
        // etapa nasce vazia passaria por inércia se a reposição não mexesse nas coleções, já
        // que elas nunca teriam saído.
        await using (SelecaoDbContext sessao = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(sessao, processoId);
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

            await sessao.SaveChangesAsync();
        }

        await using (SelecaoDbContext leitura = fixture.CreateDbContext())
        {
            EtapaProcesso suja = (await CarregarAsync(leitura, processoId)).Etapas.Single(e => e.Id == objetivaId);
            suja.Produtos.Should().BeEmpty("pré-condição: a sessão editorial esvaziou a etapa");
            suja.Recursos.Should().BeEmpty("pré-condição: a sessão editorial esvaziou a etapa");
            suja.EmiteParecerIndividual.Should().BeFalse("pré-condição: a sessão editorial retirou a promessa de parecer");
        }

        await using (SelecaoDbContext descarte = fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(descarte, processoId);

            Result<GrafoConfiguracao> prova = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(tracked, versao);
            prova.IsSuccess.Should().BeTrue(prova.Error?.Message);

            tracked.LimparColetaEDerivacaoParaRestauracao();
            await descarte.SaveChangesAsync();

            tracked.RestaurarConfiguracaoCongelada(versao, prova.Value!).IsSuccess.Should().BeTrue();
            await descarte.SaveChangesAsync();
        }

        await using SelecaoDbContext verificacao = fixture.CreateDbContext();
        ProcessoSeletivo reposto = await CarregarAsync(verificacao, processoId);
        EtapaProcesso objetivaReposta = reposto.Etapas.Single(e => e.Id == objetivaId);
        EtapaProcesso congeladaOriginal = CorpusEnvelope.ProcessoRico(Variante).Etapas.Single(e => e.Id == objetivaId);

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
                reposto, CorpusEnvelope.DadosRicos(), CorpusEnvelope.HashDocumento, FusoInstitucional.ZoneId,
                ValoresSelecionaveisCongelados: CorpusEnvelope.ValoresSelecionaveisRicos(),
                CalendarioDiasUteis: CorpusEnvelope.CalendarioRico()));

        recodificado.Value!.Bytes.Should().Equal(congelado.Bytes,
            "a etapa que o descarte devolve tem de ser a que o documento publicado descreve — janela, produtos, " +
            "bancas e janelas recursais inclusive");
    }

    private static async Task<ProcessoSeletivo> CarregarAsync(SelecaoDbContext context, Guid id) =>
        await context.ProcessosSeletivos
            // As filhas da etapa vêm explícitas, como no repositório de produção: sem elas a
            // coleção tracked nasce vazia e o agregado relido recanonicaliza com produtos,
            // bancas e recursos zerados — bytes a menos que os congelados, e a prova de que
            // "nada se perdeu no caminho" valeria só para o que o Include alcançou.
            .Include(p => p.Etapas).ThenInclude(e => e.Produtos)
            .Include(p => p.Etapas).ThenInclude(e => e.Bancas)
            .Include(p => p.Etapas).ThenInclude(e => e.Recursos)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Condicoes)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Recursos)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.TiposDeficiencia)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.VagasOfertadas)
            .Include(p => p.BonusRegional!).ThenInclude(b => b.Municipios)
            // Cascata de remanejamento (Story #575): mesmo motivo do Include de FatosColetados/
            // RegrasDerivacao logo abaixo — sem ele, a coleção tracked nasce vazia e a
            // restauração (AplicarGrafo) tentaria inserir uma ConfiguracaoCascataRemanejamento
            // nova para um ProcessoSeletivoId que já tem uma, colidindo no índice único.
            .Include(p => p.Cascata!).ThenInclude(c => c.Destinos)
            .Include(p => p.CriteriosDesempate)
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .Include(p => p.Classificacao!).ThenInclude(c => c.QuadroPesoAreaEnem).ThenInclude(g => g.Areas)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.RegraRecurso)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.Produtos)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas).ThenInclude(b => b.RecorteDeCompetencia)
            // Fatos coletados e regras de derivação (Story #928, §7.4): sem os Include, a coleção
            // tracked nasce vazia e a restauração (AplicarGrafo) inseriria linhas novas para os
            // mesmos (ProcessoSeletivoId, FatoCodigo)/(…, CodigoFato) já persistidos, colidindo no
            // índice único — a mesma razão pela qual o repositório de produção já os inclui.
            .Include(p => p.FatosColetados).ThenInclude(f => f.Precondicoes)
            .Include(p => p.RegrasDerivacao).ThenInclude(c => c.Regras).ThenInclude(r => r.Condicoes)
            // Taxa de inscrição e isenção (issue #1112) — MESMO motivo do Include de Cascata
            // acima: sem ele, a restauração (AplicarGrafo) tentaria inserir uma
            // ConfiguracaoTaxaInscricao nova para um ProcessoSeletivoId que já tem uma,
            // colidindo no índice único.
            .Include(p => p.ConfiguracaoTaxaInscricao)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == id);
}
