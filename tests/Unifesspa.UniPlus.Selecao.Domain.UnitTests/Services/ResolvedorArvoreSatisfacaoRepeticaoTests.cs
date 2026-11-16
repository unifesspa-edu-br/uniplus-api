namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ArvoreDeTeste;

/// <summary>
/// Repetição por entidade no <see cref="ResolvedorArvoreSatisfacao"/>: uma avaliação por ocorrência do grupo repetível.
/// </summary>
public sealed class ResolvedorArvoreSatisfacaoRepeticaoTests
{
    // ── Repetição por entidade (Story #922) ──────────────────────────────────────────────

    [Fact(DisplayName = "RG e CPF são pedidos de cada membro, e a certidão de guarda só do membro sob guarda")]
    public void RepeticaoPorGrupo_DocumentosPorMembroECertidaoDoMenorSobGuarda()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        DocumentoExigido cpf = DocumentoGeral("ELIMINA");
        DocumentoExigido certidaoDeGuarda = DocumentoCondicional("SOB_GUARDA", "ELIMINA");
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [],
            [NoExigencia.CriarFolha(rg, 0).Value!, NoExigencia.CriarFolha(cpf, 1).Value!, NoExigencia.CriarFolha(certidaoDeGuarda, 2).Value!],
            repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz),
            instancias: InstanciasDe(
                "COMPOSICAO_FAMILIAR", Instancia("membro_1", ("SOB_GUARDA", false)), Instancia("membro_2", ("SOB_GUARDA", true))));

        resultado.Value!.StatusPorEntidade
            .Where(static s => s.Status == StatusResolucaoExigencia.Pendente)
            .Select(static s => (s.DocumentoExigidoId, s.EntidadeId))
            .Should().BeEquivalentTo(
            [
                (rg.Id, "membro_1"), (rg.Id, "membro_2"), (cpf.Id, "membro_1"), (cpf.Id, "membro_2"), (certidaoDeGuarda.Id, "membro_2"),
            ]);
    }

    [Fact(DisplayName = "Story #926: atributo NAO_APLICAVEL da instância sobrescreve o mesmo fato resolvido do candidato")]
    public void RepeticaoPorEntidade_AtributoNaoAplicavelSobrescreveFatoDoCandidato()
    {
        DocumentoExigido condicional = DocumentoCondicional("SEM_RENDA");
        NoExigencia raiz = NoExigencia.CriarFolha(
            condicional, 0, repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;

        // O candidato declarou o fato, mas para ESTE membro da composição familiar ele não se
        // aplica — o sujeito do gatilho é a instância, não o candidato.
        Dictionary<string, FatoResolvido> fatosDoCandidato = new(StringComparer.Ordinal)
        {
            ["SEM_RENDA"] = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true)),
        };
        InstanciaEntidade membro = new("membro_1", new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)
        {
            ["SEM_RENDA"] = FatoResolvido.NaoAplicavel(),
        });

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz),
            fatos: fatosDoCandidato,
            instancias: InstanciasDe("COMPOSICAO_FAMILIAR", membro));

        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == condicional.Id
                && s.EntidadeId == "membro_1"
                && s.Status == StatusResolucaoExigencia.NaoAplicavel,
            "o atributo da instância tem precedência — sem isso, o fato do candidato exigiria o documento de um membro a quem ele não cabe");
    }

    [Fact(DisplayName = "Documento correlacionado à instância certa: RG do membro 2 satisfaz só (RG, COMPOSICAO_FAMILIAR, membro_2)")]
    public void RepeticaoPorEntidade_DocumentoCorrelacionadoAInstanciaCerta()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        NoExigencia raiz = NoExigencia.CriarFolha(rg, 0, repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;

        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            "COMPOSICAO_FAMILIAR", Instancia("membro_1"), Instancia("membro_2"), Instancia("membro_3"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaDeEntidade(rg, "membro_2"), instancias: instancias);

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
        resultado.Value!.ConsequenciasVigentes.Should().HaveCount(2);
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.EntidadeId == "membro_1" && c.Consequencia == "ELIMINA");
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.EntidadeId == "membro_3" && c.Consequencia == "ELIMINA");
        resultado.Value!.ConsequenciasVigentes.Should().NotContain(c => c.EntidadeId == "membro_2");
        resultado.Value!.StatusPorEntidade.Should().HaveCount(3);
        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == rg.Id && s.EntidadeId == "membro_1" && s.Status == StatusResolucaoExigencia.Pendente);
        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == rg.Id && s.EntidadeId == "membro_2" && s.Status == StatusResolucaoExigencia.Satisfeita);
        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == rg.Id && s.EntidadeId == "membro_3" && s.Status == StatusResolucaoExigencia.Pendente);
    }

    [Fact(DisplayName = "Repetição por entidade: folha sem ConsequenciaIndeferimento continua visível por instância via StatusPorEntidade")]
    public void RepeticaoPorEntidade_SemConsequencia_StatusPorEntidadeSinalizaInstanciaPendente()
    {
        DocumentoExigido comprovante = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(comprovante, 0, repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;

        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            "COMPOSICAO_FAMILIAR", Instancia("membro_1"), Instancia("membro_2"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz), apresentacoes: ApresentaDeEntidade(comprovante, "membro_1"), instancias: instancias);

        // Sem consequência configurada, nada é emitido em ConsequenciasVigentes — o único
        // sinal de QUAL instância está pendente é StatusPorEntidade.
        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == comprovante.Id && s.EntidadeId == "membro_1" && s.Status == StatusResolucaoExigencia.Satisfeita);
        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == comprovante.Id && s.EntidadeId == "membro_2" && s.Status == StatusResolucaoExigencia.Pendente);
    }

    [Fact(DisplayName = "Gatilho por atributo da entidade: declaração de isento só é exigida do membro adulto e sem renda")]
    public void RepeticaoPorEntidade_GatilhoPorAtributoDaEntidade()
    {
        DocumentoExigido declaracaoIsento = DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "DECL_ISENTO", "Declaração de isento", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: false, "ELIMINA",
            [
                CondicaoGatilho.Criar(0, "MAIOR_IDADE", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
                CondicaoGatilho.Criar(0, "SEM_RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
            ],
            [], null, Qualquer, null).Value!;
        NoExigencia raiz = NoExigencia.CriarFolha(declaracaoIsento, 0, repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;

        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            "COMPOSICAO_FAMILIAR",
            Instancia("membro_adulto_sem_renda", ("MAIOR_IDADE", true), ("SEM_RENDA", true)),
            Instancia("membro_adulto_com_renda", ("MAIOR_IDADE", true), ("SEM_RENDA", false)),
            Instancia("membro_menor", ("MAIOR_IDADE", false), ("SEM_RENDA", true)));

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), instancias: instancias);

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Pendente);
        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.EntidadeId == "membro_adulto_sem_renda" && c.Consequencia == "ELIMINA");
    }

    [Fact(DisplayName = "PF + PJ vinculada repete por empresa: extrato da PJ 1 satisfaz só essa PJ — PJ 2 continua com extrato e IRPJ pendentes")]
    public void RepeticaoPorEntidade_PessoaJuridicaVinculada_CorrelacaoPorPj()
    {
        DocumentoExigido extrato = DocumentoGeral("ELIMINA");
        DocumentoExigido irpj = DocumentoGeral("ELIMINA");
        NoExigencia folhaExtrato = NoExigencia.CriarFolha(extrato, 0).Value!;
        NoExigencia folhaIrpj = NoExigencia.CriarFolha(irpj, 1).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [folhaExtrato, folhaIrpj],
            repetePorEntidade: "PESSOAS_JURIDICAS").Value!;

        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            "PESSOAS_JURIDICAS", Instancia("pj_1"), Instancia("pj_2"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(grupo), apresentacoes: ApresentaDeEntidade(extrato, "pj_1"), instancias: instancias);

        resultado.Value!.EstadosPorNo[grupo.Id].Should().Be(EstadoSatisfacao.Pendente);
        resultado.Value!.ConsequenciasVigentes.Should().HaveCount(3);
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.EntidadeId == "pj_1" && c.NoExigenciaId == folhaIrpj.Id);
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.EntidadeId == "pj_2" && c.NoExigenciaId == folhaExtrato.Id);
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.EntidadeId == "pj_2" && c.NoExigenciaId == folhaIrpj.Id);
        resultado.Value!.ConsequenciasVigentes.Should().NotContain(c => c.EntidadeId == "pj_1" && c.NoExigenciaId == folhaExtrato.Id);
    }

    [Fact(DisplayName = "Repetição por entidade sem nenhuma instância declarada é não-aplicável")]
    public void RepeticaoPorEntidade_SemInstanciasDeclaradas_NaoAplicavel()
    {
        DocumentoExigido documento = DocumentoGeral("ELIMINA");
        NoExigencia raiz = NoExigencia.CriarFolha(documento, 0, repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz));

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.NaoAplicavel);
        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Repetição por entidade: todas as instâncias satisfeitas suprime consequências")]
    public void RepeticaoPorEntidade_TodasSatisfeitas_Suprime()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        NoExigencia raiz = NoExigencia.CriarFolha(rg, 0, repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;

        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            "COMPOSICAO_FAMILIAR", Instancia("membro_1"), Instancia("membro_2"));

        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = new()
        {
            [rg.Id] =
            [
                new ApresentacaoDocumento(Guid.CreateVersion7(), EntidadeId: "membro_1"),
                new ApresentacaoDocumento(Guid.CreateVersion7(), EntidadeId: "membro_2"),
            ],
        };

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), apresentacoes: apresentacoes, instancias: instancias);

        resultado.Value!.EstadosPorNo[raiz.Id].Should().Be(EstadoSatisfacao.Satisfeito);
        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Repetição por entidade: PendenciasDeOrientacao de um grupo OU opaco repetido carrega o EntidadeId correto por instância")]
    public void RepeticaoPorEntidade_GrupoOuOpaco_PendenciasDeOrientacaoComEntidadeId()
    {
        DocumentoExigido docA = DocumentoGeral();
        DocumentoExigido docB = DocumentoGeral();
        NoExigencia folhaA = NoExigencia.CriarFolha(docA, 0).Value!;
        NoExigencia folhaB = NoExigencia.CriarFolha(docB, 1).Value!;
        NoExigencia grupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 2, "ELIMINA", [BaseLegalResolvida()], [folhaA, folhaB],
            repetePorEntidade: "PESSOAS_JURIDICAS").Value!;

        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            "PESSOAS_JURIDICAS", Instancia("pj_1"), Instancia("pj_2"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(grupo), apresentacoes: ApresentaDeEntidade(docA, "pj_1"), instancias: instancias);

        resultado.Value!.PendenciasDeOrientacao.Should().HaveCount(3);
        resultado.Value!.PendenciasDeOrientacao.Should().Contain(p => p.NoExigenciaId == folhaB.Id && p.EntidadeId == "pj_1");
        resultado.Value!.PendenciasDeOrientacao.Should().Contain(p => p.NoExigenciaId == folhaA.Id && p.EntidadeId == "pj_2");
        resultado.Value!.PendenciasDeOrientacao.Should().Contain(p => p.NoExigenciaId == folhaB.Id && p.EntidadeId == "pj_2");
        resultado.Value!.ConsequenciasVigentes.Should().HaveCount(2);
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.NoExigenciaId == grupo.Id && c.EntidadeId == "pj_1");
        resultado.Value!.ConsequenciasVigentes.Should().Contain(c => c.NoExigenciaId == grupo.Id && c.EntidadeId == "pj_2");
    }

    [Fact(DisplayName = "Árvore congelada com repetição por entidade aninhada (só alcançável via Reidratar) é recusada")]
    public void RepeticaoPorEntidade_AninhadaViaReidratar_Recusa()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia folhaInterna = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.Folha, 0, documento.Id, documento, null, null, null, null, null,
            "PESSOAS_JURIDICAS", [], []);
        NoExigencia grupoExterno = NoExigencia.Reidratar(
            Guid.CreateVersion7(), TipoNo.GrupoE, 0, null, null, null, null, null, null, null,
            "COMPOSICAO_FAMILIAR", [], [folhaInterna]);

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(grupoExterno));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ResolvedorArvoreSatisfacao.ArvoreEstruturalmenteInvalida");
    }

    [Fact(DisplayName = "Instâncias de entidade com EntidadeId duplicado são recusadas")]
    public void RepeticaoPorEntidade_InstanciasComIdDuplicado_Recusa()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(documento, 0, repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;
        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            "COMPOSICAO_FAMILIAR", Instancia("membro_1"), Instancia("membro_1"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), instancias: instancias);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ResolvedorArvoreSatisfacao.InstanciaEntidadeInvalida");
    }

    [Fact(DisplayName = "Instância de entidade com EntidadeId vazio é recusada")]
    public void RepeticaoPorEntidade_InstanciaComIdVazio_Recusa()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia raiz = NoExigencia.CriarFolha(documento, 0, repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;
        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            "COMPOSICAO_FAMILIAR", Instancia(""));

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), instancias: instancias);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ResolvedorArvoreSatisfacao.InstanciaEntidadeInvalida");
    }
}
