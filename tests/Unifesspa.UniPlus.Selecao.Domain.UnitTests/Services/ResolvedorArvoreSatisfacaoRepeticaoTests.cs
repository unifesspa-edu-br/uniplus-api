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

using static ApoioDaArvoreDeSatisfacao;

public sealed class ResolvedorArvoreSatisfacaoRepeticaoTests
{
    [Fact(DisplayName = "RG e CPF são pedidos de cada membro, e a certidão de guarda só do membro sob guarda")]
    public void RepeticaoPorGrupo_DocumentosPorMembroECertidaoDoMenorSobGuarda()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        DocumentoExigido cpf = DocumentoGeral("ELIMINA");
        DocumentoExigido certidaoDeGuarda = DocumentoCondicional("SOB_GUARDA", "ELIMINA");
        NoExigencia raiz = GrupoFamiliar(rg, cpf, certidaoDeGuarda);

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(raiz),
            instancias: InstanciasDe(
                "COMPOSICAO_FAMILIAR", Instancia("membro_1", ("SOB_GUARDA", false)), Instancia("membro_2", ("SOB_GUARDA", true))));

        ConferirDocumentosPendentesDosMembros(resultado.Value!, rg, cpf, certidaoDeGuarda);
    }

    [Fact(DisplayName = "Story #926: atributo NAO_APLICAVEL da instância sobrescreve o mesmo fato resolvido do candidato")]
    public void RepeticaoPorEntidade_AtributoNaoAplicavelSobrescreveFatoDoCandidato()
    {
        DocumentoExigido condicional = DocumentoCondicional("SEM_RENDA");
        NoExigencia raiz = NoExigencia.CriarFolha(condicional, 0, repetePorEntidade: "COMPOSICAO_FAMILIAR").Value!;

        // O candidato declarou o fato, mas para ESTE membro da composição familiar ele não se
        // aplica — o sujeito do gatilho é a instância, não o candidato.
        Dictionary<string, FatoResolvido> fatosDoCandidato = Fatos("SEM_RENDA", true);
        InstanciaEntidade membro = new("membro_1", new Dictionary<string, FatoResolvido>(StringComparer.Ordinal) { ["SEM_RENDA"] = FatoResolvido.NaoAplicavel() });

        Result<ResultadoResolucaoArvore> resultado = Resolver(Arvore(raiz), fatosDoCandidato, instancias: InstanciasDe("COMPOSICAO_FAMILIAR", membro));

        resultado.Value!.StatusPorEntidade.Should().Contain(
            s => s.DocumentoExigidoId == condicional.Id && s.EntidadeId == "membro_1" && s.Status == StatusResolucaoExigencia.NaoAplicavel);
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
        resultado.Value.ConsequenciasVigentes.Select(static c => (c.EntidadeId, c.Consequencia))
            .Should().BeEquivalentTo([("membro_1", "ELIMINA"), ("membro_3", "ELIMINA")]);
        ConferirStatusDosMembros(resultado.Value, rg.Id, "membro_2", ["membro_1", "membro_3"]);
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
        ConferirStatusDosMembros(resultado.Value!, comprovante.Id, "membro_1", ["membro_2"]);
    }

    [Fact(DisplayName = "Gatilho por atributo da entidade: declaração de isento só é exigida do membro adulto e sem renda")]
    public void RepeticaoPorEntidade_GatilhoPorAtributoDaEntidade()
    {
        DocumentoExigido declaracaoIsento = DeclaracaoDeIsento();
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
        NoExigencia grupo = GrupoJuridico(folhaExtrato, folhaIrpj);

        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            "PESSOAS_JURIDICAS", Instancia("pj_1"), Instancia("pj_2"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(grupo), apresentacoes: ApresentaDeEntidade(extrato, "pj_1"), instancias: instancias);

        resultado.Value!.EstadosPorNo[grupo.Id].Should().Be(EstadoSatisfacao.Pendente);
        resultado.Value.ConsequenciasVigentes.Select(static c => (c.EntidadeId, c.NoExigenciaId)).Should().BeEquivalentTo([("pj_1", folhaIrpj.Id), ("pj_2", folhaExtrato.Id), ("pj_2", folhaIrpj.Id)]);
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

        Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes = ApresentacoesDosMembros(rg, "membro_1", "membro_2");

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
        NoExigencia grupo = GrupoOuJuridico(folhaA, folhaB);

        Dictionary<string, IReadOnlyList<InstanciaEntidade>> instancias = InstanciasDe(
            "PESSOAS_JURIDICAS", Instancia("pj_1"), Instancia("pj_2"));

        Result<ResultadoResolucaoArvore> resultado = Resolver(
            Arvore(grupo), apresentacoes: ApresentaDeEntidade(docA, "pj_1"), instancias: instancias);

        ConferirOrientacoesDoGrupo(resultado.Value!, grupo, folhaA, folhaB);
    }

    [Fact(DisplayName = "Árvore congelada com repetição por entidade aninhada (só alcançável via Reidratar) é recusada")]
    public void RepeticaoPorEntidade_AninhadaViaReidratar_Recusa()
    {
        DocumentoExigido documento = DocumentoGeral();
        NoExigencia folhaInterna = NoExigencia.ReidratarFolha(
            Guid.CreateVersion7(), 0, documento, null, null, "PESSOAS_JURIDICAS");
        NoExigencia grupoExterno = NoExigencia.ReidratarGrupo(
            Guid.CreateVersion7(), 0, new CombinacaoDoGrupo(TipoNo.GrupoE, null, null), [], [folhaInterna], "COMPOSICAO_FAMILIAR");

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

    private static void ConferirDocumentosPendentesDosMembros(
        ResultadoResolucaoArvore resultado, DocumentoExigido rg, DocumentoExigido cpf, DocumentoExigido certidao)
    {
        resultado.StatusPorEntidade.Where(static s => s.Status == StatusResolucaoExigencia.Pendente)
            .Select(static s => (s.DocumentoExigidoId, s.EntidadeId)).Should().BeEquivalentTo(
                [(rg.Id, "membro_1"), (rg.Id, "membro_2"), (cpf.Id, "membro_1"), (cpf.Id, "membro_2"), (certidao.Id, "membro_2")]);
    }
    private static void ConferirStatusDosMembros(ResultadoResolucaoArvore resultado, Guid documentoId, string satisfeito, string[] pendentes)
    {
        resultado.StatusPorEntidade.Select(static s => (s.DocumentoExigidoId, s.EntidadeId, s.Status)).Should().BeEquivalentTo(
            pendentes.Select(id => (documentoId, id, StatusResolucaoExigencia.Pendente))
                .Append((documentoId, satisfeito, StatusResolucaoExigencia.Satisfeita)));
    }
    private static DocumentoExigido DeclaracaoDeIsento() => DocumentoExigido.Criar(
            FaseId, Guid.CreateVersion7(), "DECL_ISENTO", "Declaração de isento", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Condicional, obrigatorio: false, "ELIMINA",
            [
                CondicaoGatilho.Criar(0, "MAIOR_IDADE", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
                CondicaoGatilho.Criar(0, "SEM_RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
            ],
            [], null, Qualquer, null).Value!;
    private static NoExigencia GrupoFamiliar(params DocumentoExigido[] documentos) => NoExigencia.CriarGrupo(
        TipoNo.GrupoE, 0, null, null, [], [.. documentos.Select(static (d, i) => NoExigencia.CriarFolha(d, i).Value!)], "COMPOSICAO_FAMILIAR").Value!;

    private static NoExigencia GrupoJuridico(params NoExigencia[] folhas) =>
        NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], folhas, "PESSOAS_JURIDICAS").Value!;

    private static NoExigencia GrupoOuJuridico(params NoExigencia[] folhas) =>
        NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, 2, "ELIMINA", [BaseLegalResolvida()], folhas, "PESSOAS_JURIDICAS").Value!;

    private static Dictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> ApresentacoesDosMembros(DocumentoExigido documento, params string[] membros) =>
        new() { [documento.Id] = [.. membros.Select(static membro => new ApresentacaoDocumento(Guid.CreateVersion7(), EntidadeId: membro))] };

    private static void ConferirOrientacoesDoGrupo(ResultadoResolucaoArvore resultado, NoExigencia grupo, NoExigencia folhaA, NoExigencia folhaB)
    {
        resultado.PendenciasDeOrientacao.Select(static p => (p.NoExigenciaId, p.EntidadeId)).Should()
            .BeEquivalentTo([(folhaB.Id, "pj_1"), (folhaA.Id, "pj_2"), (folhaB.Id, "pj_2")]);
        resultado.ConsequenciasVigentes.Select(static c => (c.NoExigenciaId, c.EntidadeId)).Should()
            .BeEquivalentTo([(grupo.Id, "pj_1"), (grupo.Id, "pj_2")]);
    }
}
