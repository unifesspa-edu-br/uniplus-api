namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
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

            Result resultado = new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(tracked, versao);
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

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
            new EntradaCanonicalizacao(reposto, CorpusEnvelope.DadosRicos(), CorpusEnvelope.HashDocumento));

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
            new RestauradorDeConfiguracao(CorpusEnvelope.Registro).Restaurar(tracked, versao)
                .IsSuccess.Should().BeTrue();
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

        // ...e o CreatedAt não voltou, porque nunca saiu: é a MESMA linha (ADR-0110 D2).
        reconciliada.CreatedAt.Should().Be(criadoEm,
            "a etapa reconciliada é a mesma linha — a D2 declara que ela preserva o CreatedAt original, ao contrário " +
            "das demais filhas, que são recriadas e recebem o instante do descarte");
    }

    private static async Task<ProcessoSeletivo> CarregarAsync(SelecaoDbContext context, Guid id) =>
        await context.ProcessosSeletivos
            .Include(p => p.Etapas)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Condicoes)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.Recursos)
            .Include(p => p.OfertaAtendimento!).ThenInclude(o => o.TiposDeficiencia)
            .Include(p => p.DistribuicaoVagas).ThenInclude(d => d.Modalidades)
            .Include(p => p.BonusRegional)
            .Include(p => p.CriteriosDesempate)
            .Include(p => p.Classificacao!).ThenInclude(c => c.RegrasEliminacao)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.RegraRecurso)
            .Include(p => p.CronogramaFases).ThenInclude(f => f.BancasRequeridas)
            .AsSplitQuery()
            .FirstAsync(p => p.Id == id);
}
