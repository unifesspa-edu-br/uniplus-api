namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Story #927 — o motor de derivação contra a matriz R0–R9 de MODALIDADE (Lei 12.711). A tabela de
/// combinações alcançáveis da spec entra literalmente como oráculo: cada perfil de respostas produz
/// exatamente o conjunto normativo (antes da interseção com a oferta, que é passo da classificação).
/// </summary>
/// <remarks>
/// São dezessete casos de motor: a tabela da spec tem dezoito linhas, mas a linha de
/// <c>COR_RACA=NAO_INFORMADO</c> é sobre o <b>resolvedor de coleta</b> decidir se o bloco quilombola é
/// apresentado — o motor recebe <c>CONCORRER_Q</c> já resolvido, e essa linha é coberta pelos testes
/// do resolvedor, não aqui.
/// </remarks>
public sealed class MotorDerivacaoModalidadeTests
{
    private static readonly RegrasDerivacaoFato Ruleset = RegrasDerivacaoModalidadeLei12711.Construir();

    private static FatoResolvido Sim => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(true));

    private static FatoResolvido Nao => FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(false));

    private static FatoResolvido NaoAplicavel => FatoResolvido.NaoAplicavel();

    private static FatoResolvido Indeterminado => FatoResolvido.Indeterminado();

    /// <summary>
    /// Monta o dicionário de fatos com defaults coerentes com a coleta: sem escola pública, os
    /// opt-ins gatados por ela são não-aplicáveis; o teste sobrescreve o que o perfil exige.
    /// </summary>
    private static Dictionary<string, FatoResolvido> Fatos(params (string Fato, FatoResolvido Estado)[] overrides)
    {
        Dictionary<string, FatoResolvido> fatos = new(StringComparer.Ordinal)
        {
            ["CONCORRER_PCD"] = Nao,
            ["EGRESSO_ESCOLA_PUBLICA"] = Nao,
            ["CONCORRER_EP"] = NaoAplicavel,
            ["CONCORRER_PPI"] = NaoAplicavel,
            ["CONCORRER_Q"] = NaoAplicavel,
            ["CONCORRER_RENDA"] = NaoAplicavel,
        };
        foreach ((string fato, FatoResolvido estado) in overrides)
        {
            fatos[fato] = estado;
        }

        return fatos;
    }

    private static string[] Derivar(Dictionary<string, FatoResolvido> fatos)
    {
        ResultadoDerivacao resultado = MotorDerivacao.Derivar(Ruleset, fatos);
        resultado.Estado.Should().Be(EstadoFato.Resolvido);
        return [.. resultado.Valores.OrderBy(v => v, StringComparer.Ordinal)];
    }

    // ── Tabela normativa de combinações alcançáveis (spec) ──────────────────────────────

    [Fact(DisplayName = "Nenhuma cota / todos opt-out → {AC}")]
    public void NenhumaCota_SoAC() =>
        Derivar(Fatos()).Should().Equal("AC");

    [Fact(DisplayName = "PcD opt-in, não escola pública → {AC, AC_PCD}")]
    public void PcdSemEscolaPublica_AcEAcPcd() =>
        Derivar(Fatos(("CONCORRER_PCD", Sim))).Should().Equal("AC", "AC_PCD");

    [Fact(DisplayName = "PcD opt-in, escola pública, sem renda → {AC, AC_PCD, LI_PCD}")]
    public void PcdEscolaPublicaSemRenda() =>
        Derivar(Fatos(
            ("CONCORRER_PCD", Sim), ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_RENDA", Nao)))
            .Should().Equal("AC", "AC_PCD", "LI_PCD");

    [Fact(DisplayName = "PcD opt-in, escola pública, +renda → {AC, AC_PCD, LI_PCD, LB_PCD}")]
    public void PcdEscolaPublicaComRenda() =>
        Derivar(Fatos(
            ("CONCORRER_PCD", Sim), ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_RENDA", Sim)))
            .Should().Equal("AC", "AC_PCD", "LB_PCD", "LI_PCD");

    [Fact(DisplayName = "Escola pública + CONCORRER_EP, sem renda → {AC, LI_EP}")]
    public void EscolaPublicaEpSemRenda() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_EP", Sim), ("CONCORRER_RENDA", Nao)))
            .Should().Equal("AC", "LI_EP");

    [Fact(DisplayName = "Escola pública + CONCORRER_EP, +renda → {AC, LI_EP, LB_EP}")]
    public void EscolaPublicaEpComRenda() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_EP", Sim), ("CONCORRER_RENDA", Sim)))
            .Should().Equal("AC", "LB_EP", "LI_EP");

    [Fact(DisplayName = "Escola pública, CONCORRER_EP=NÃO, sem outra dimensão → {AC}")]
    public void EscolaPublicaEpNao_SoAC() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_EP", Nao), ("CONCORRER_RENDA", Nao)))
            .Should().Equal("AC");

    [Fact(DisplayName = "PPI + CONCORRER_PPI, escola pública, sem renda → {AC, LI_PPI}")]
    public void PpiEscolaPublicaSemRenda() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_PPI", Sim), ("CONCORRER_RENDA", Nao)))
            .Should().Equal("AC", "LI_PPI");

    [Fact(DisplayName = "PPI + CONCORRER_PPI, escola pública, +renda → {AC, LI_PPI, LB_PPI}")]
    public void PpiEscolaPublicaComRenda() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_PPI", Sim), ("CONCORRER_RENDA", Sim)))
            .Should().Equal("AC", "LB_PPI", "LI_PPI");

    [Fact(DisplayName = "PPI-elegível opta por EP em vez de PPI → {AC, LI_EP}")]
    public void PpiOptOutEpOptIn() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_PPI", Nao), ("CONCORRER_EP", Sim), ("CONCORRER_RENDA", Nao)))
            .Should().Equal("AC", "LI_EP");

    [Fact(DisplayName = "Quilombola + CONCORRER_Q, escola pública, sem renda → {AC, LI_Q}")]
    public void QuilombolaEscolaPublicaSemRenda() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_Q", Sim), ("CONCORRER_RENDA", Nao)))
            .Should().Equal("AC", "LI_Q");

    [Fact(DisplayName = "Quilombola + CONCORRER_Q, escola pública, +renda → {AC, LI_Q, LB_Q}")]
    public void QuilombolaEscolaPublicaComRenda() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_Q", Sim), ("CONCORRER_RENDA", Sim)))
            .Should().Equal("AC", "LB_Q", "LI_Q");

    [Fact(DisplayName = "Quilombola-elegível opta por EP em vez de Q → {AC, LI_EP}")]
    public void QuilombolaOptOutEpOptIn() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_Q", Nao), ("CONCORRER_EP", Sim), ("CONCORRER_RENDA", Nao)))
            .Should().Equal("AC", "LI_EP");

    [Fact(DisplayName = "CONCORRER_Q não-aplicável (não quilombola) não concede cota Q")]
    public void ConcorrerQNaoAplicavel_SemCotaQ() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_Q", NaoAplicavel), ("CONCORRER_EP", Sim), ("CONCORRER_RENDA", Nao)))
            .Should().Equal("AC", "LI_EP");

    [Fact(DisplayName = "PPI + Q simultâneos (preto/pardo quilombola), escola pública, sem renda → {AC, LI_PPI, LI_Q}")]
    public void PpiEQSimultaneos() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_PPI", Sim), ("CONCORRER_Q", Sim), ("CONCORRER_RENDA", Nao)))
            .Should().Equal("AC", "LI_PPI", "LI_Q");

    [Fact(DisplayName = "PPI + Q simultâneos, +renda → {AC, LI_PPI, LB_PPI, LI_Q, LB_Q}")]
    public void PpiEQSimultaneosComRenda() =>
        Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_PPI", Sim), ("CONCORRER_Q", Sim), ("CONCORRER_RENDA", Sim)))
            .Should().Equal("AC", "LB_PPI", "LB_Q", "LI_PPI", "LI_Q");

    [Fact(DisplayName = "PcD + PPI + EP, escola pública, +renda → união das oito modalidades")]
    public void PcdPpiEp_UniaoCompleta() =>
        Derivar(Fatos(
            ("CONCORRER_PCD", Sim), ("EGRESSO_ESCOLA_PUBLICA", Sim),
            ("CONCORRER_EP", Sim), ("CONCORRER_PPI", Sim), ("CONCORRER_RENDA", Sim)))
            .Should().Equal("AC", "AC_PCD", "LB_EP", "LB_PCD", "LB_PPI", "LI_EP", "LI_PCD", "LI_PPI");

    // ── Regras estruturais do motor ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Dependência indeterminada torna o derivado indeterminado (fail-closed)")]
    public void DependenciaIndeterminada_DerivadoIndeterminado()
    {
        Dictionary<string, FatoResolvido> fatos = Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_PPI", Sim), ("CONCORRER_RENDA", Indeterminado));

        ResultadoDerivacao resultado = MotorDerivacao.Derivar(Ruleset, fatos);

        resultado.Estado.Should().Be(
            EstadoFato.Indeterminado,
            "um dependente indeterminado bloqueia o derivado inteiro — não assume valor parcial");
        resultado.Valores.Should().BeEmpty();
    }

    [Fact(DisplayName = "Fail-closed pós-gate: dependente resolvido com valor de tipo incoerente torna o derivado indeterminado")]
    public void DependenteResolvidoComTipoIncoerente_DerivadoIndeterminado()
    {
        // CONCORRER_PPI resolvido como STRING em vez de booleano: passa o gate (estado Resolvido),
        // mas o predicado da regra R6 avalia indeterminado (tipo incoerente com o operador). O motor
        // não pode tratar a regra como inativa e devolver {AC} resolvido — seria decidir sobre dado
        // corrompido. Deve propagar indeterminado.
        Dictionary<string, FatoResolvido> fatos = Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim),
            ("CONCORRER_PPI", FatoResolvido.Resolvido(JsonSerializer.SerializeToElement("SIM"))),
            ("CONCORRER_RENDA", Nao));

        ResultadoDerivacao resultado = MotorDerivacao.Derivar(Ruleset, fatos);

        resultado.Estado.Should().Be(
            EstadoFato.Indeterminado,
            "um valor resolvido que o motor não sabe comparar não pode ser silenciosamente tratado como regra inativa");
        resultado.Valores.Should().BeEmpty();
    }

    [Fact(DisplayName = "Dependência ausente do dicionário também torna o derivado indeterminado")]
    public void DependenciaAusente_DerivadoIndeterminado()
    {
        Dictionary<string, FatoResolvido> fatos = new(StringComparer.Ordinal)
        {
            ["CONCORRER_PCD"] = Sim,
            // faltam as demais dependências declaradas
        };

        MotorDerivacao.Derivar(Ruleset, fatos).Estado.Should().Be(EstadoFato.Indeterminado);
    }

    [Fact(DisplayName = "Opt-out de renda (não-aplicável) mantém só as independentes de renda")]
    public void OptOutRenda_SoLI()
    {
        // Renda não-aplicável é informação resolvida: não bloqueia o derivado; só faz as LB_* não
        // contribuírem (a cláusula com CONCORRER_RENDA colapsa falso pelo átomo não-aplicável).
        string[] derivado = Derivar(Fatos(
            ("EGRESSO_ESCOLA_PUBLICA", Sim), ("CONCORRER_PPI", Sim), ("CONCORRER_RENDA", NaoAplicavel)));

        derivado.Should().Equal("AC", "LI_PPI");
        derivado.Should().NotContain("LB_PPI");
    }

    [Fact(DisplayName = "A âncora resolve mesmo com todo o resto não-aplicável — o conjunto nunca é vazio por dependência resolvida")]
    public void Ancora_ResolveComRestoNaoAplicavel() =>
        Derivar(Fatos()).Should().Equal("AC");

    [Fact(DisplayName = "O conjunto resolvido é congelado — mutar a entrada depois não altera o resultado")]
    public void ResultadoResolvido_Congelado()
    {
        HashSet<string> entrada = new(["AC", "LI_PPI"], StringComparer.Ordinal);

        ResultadoDerivacao resultado = ResultadoDerivacao.Resolvido(entrada);
        entrada.Add("CONTAMINADO");

        resultado.Valores.Should().Equal("AC", "LI_PPI");
        resultado.Valores.Should().NotContain("CONTAMINADO", "o conjunto é congelado na construção, sem compartilhar referência com a entrada");
    }

    [Fact(DisplayName = "União é idempotente — nenhum código aparece duas vezes")]
    public void Uniao_Idempotente()
    {
        string[] derivado = Derivar(Fatos(
            ("CONCORRER_PCD", Sim), ("EGRESSO_ESCOLA_PUBLICA", Sim),
            ("CONCORRER_EP", Sim), ("CONCORRER_PPI", Sim), ("CONCORRER_Q", Sim), ("CONCORRER_RENDA", Sim)));

        derivado.Should().OnlyHaveUniqueItems();
    }

    // ── Validações de cadastro da regra ─────────────────────────────────────────────────

    [Fact(DisplayName = "Código contribuído fora do domínio do fato é recusado no cadastro")]
    public void ContribuiForaDoDominio_Recusado()
    {
        RegraDerivacao regraV = RegraDerivacao.Criar(
            PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, "V").Value!;

        Result<RegrasDerivacaoFato> resultado = RegrasDerivacaoFato.Criar(
            "MODALIDADE", [regraV], dependenciasDeclaradas: [], RegrasDerivacaoModalidadeLei12711.DominioCanonico);

        resultado.IsFailure.Should().BeTrue("V não é código canônico — a modalidade PcD fora da reserva é AC_PCD");
        resultado.Error!.Code.Should().Be(RegrasDerivacaoFatoErrorCodes.ContribuiForaDoDominio);
    }

    [Fact(DisplayName = "AC_PCD é aceito como código canônico")]
    public void AcPcd_Aceito()
    {
        RegraDerivacao regra = RegraDerivacao.Criar(
            PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, "AC_PCD").Value!;

        RegrasDerivacaoFato.Criar(
            "MODALIDADE", [regra], dependenciasDeclaradas: [], RegrasDerivacaoModalidadeLei12711.DominioCanonico)
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Dependência declarada a mais é recusada (deve ser exatamente a união dos citados)")]
    public void DependenciaDeclaradaAMais_Recusada()
    {
        RegraDerivacao regra = Regra("LI_EP", ("EGRESSO_ESCOLA_PUBLICA", true), ("CONCORRER_EP", true));

        Result<RegrasDerivacaoFato> resultado = RegrasDerivacaoFato.Criar(
            "MODALIDADE",
            [regra],
            dependenciasDeclaradas: ["EGRESSO_ESCOLA_PUBLICA", "CONCORRER_EP", "CONCORRER_RENDA"],
            RegrasDerivacaoModalidadeLei12711.DominioCanonico);

        resultado.IsFailure.Should().BeTrue(
            "CONCORRER_RENDA foi declarado mas nenhuma regra o cita — bloquearia o motor por um fato que ninguém usa");
        resultado.Error!.Code.Should().Be(RegrasDerivacaoFatoErrorCodes.DependenciasIncoerentes);
    }

    [Fact(DisplayName = "Derivação que cita o próprio fato é recusada — um derivado não depende de si mesmo")]
    public void DerivacaoAutorreferente_Recusada()
    {
        // Uma regra de MODALIDADE cujo quando cita MODALIDADE: o motor exigiria MODALIDADE resolvida
        // para computar MODALIDADE, deixando-a indeterminada para sempre.
        RegraDerivacao regra = Regra("AC", ("MODALIDADE", true));

        Result<RegrasDerivacaoFato> resultado = RegrasDerivacaoFato.Criar(
            "MODALIDADE",
            [regra],
            dependenciasDeclaradas: ["MODALIDADE"],
            RegrasDerivacaoModalidadeLei12711.DominioCanonico);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RegrasDerivacaoFatoErrorCodes.DerivacaoAutorreferente);
    }

    [Fact(DisplayName = "Dependência citada mas não declarada é recusada")]
    public void DependenciaCitadaNaoDeclarada_Recusada()
    {
        RegraDerivacao regra = Regra("LI_EP", ("EGRESSO_ESCOLA_PUBLICA", true), ("CONCORRER_EP", true));

        Result<RegrasDerivacaoFato> resultado = RegrasDerivacaoFato.Criar(
            "MODALIDADE",
            [regra],
            dependenciasDeclaradas: ["EGRESSO_ESCOLA_PUBLICA"],
            RegrasDerivacaoModalidadeLei12711.DominioCanonico);

        resultado.IsFailure.Should().BeTrue("CONCORRER_EP é citado mas não declarado");
        resultado.Error!.Code.Should().Be(RegrasDerivacaoFatoErrorCodes.DependenciasIncoerentes);
    }

    private static RegraDerivacao Regra(string contribui, params (string Fato, bool Valor)[] atomos)
    {
        List<(int Clausula, CondicaoDnf Condicao)> linhas = [.. atomos
            .Select(a => (Clausula: 1, Condicao: CondicaoDnf.Criar(
                a.Fato, Operador.Igual, JsonSerializer.SerializeToElement(a.Valor)).Value!))];

        return RegraDerivacao.Criar(PredicadoDnf.CriarDeCondicoesAgrupadas(linhas).Value!, contribui).Value!;
    }
}
