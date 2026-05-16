namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura unit do <see cref="ValidadorConformidadeEdital"/> conforme
/// CA-06 da Story #459. As 4 variantes core
/// (<see cref="EtapaObrigatoria"/>, <see cref="ModalidadesMinimas"/>,
/// <see cref="DocumentoObrigatorioParaModalidade"/>,
/// <see cref="DesempateDeveIncluir"/>) recebem 3 cenários cada
/// (Aprovada/Reprovada/Malformada). As 4 variantes não-core
/// recebem smoke. Cobertura exaustiva é follow-up registrado antes do merge.
/// </summary>
public sealed class ValidadorConformidadeEditalTests
{
    // ─── EtapaObrigatoria ───────────────────────────────────────────────

    [Fact(DisplayName = "EtapaObrigatoria aprova quando etapa presente no edital")]
    public void EtapaObrigatoria_Aprovada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            CodigosTiposEtapaPresentes = ["TipoEtapaA", "TipoEtapaB"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new EtapaObrigatoria("TipoEtapaA"));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras.Should().HaveCount(1);
        resultado.Regras[0].Aprovada.Should().BeTrue();
        resultado.Regras[0].RegraCodigo.Should().Be(regra.RegraCodigo);
    }

    [Fact(DisplayName = "EtapaObrigatoria reprova quando etapa ausente")]
    public void EtapaObrigatoria_Reprovada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            CodigosTiposEtapaPresentes = ["TipoEtapaB"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new EtapaObrigatoria("TipoEtapaA"));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeFalse();
        resultado.Regras[0].DescricaoHumana.Should().Contain("TipoEtapaA");
    }

    [Fact(DisplayName = "EtapaObrigatoria reprova quando TipoEtapaCodigo vazio (malformado)")]
    public void EtapaObrigatoria_Malformada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            CodigosTiposEtapaPresentes = ["TipoEtapaA"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new EtapaObrigatoria("   "));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeFalse();
        resultado.Regras[0].DescricaoHumana.Should().Contain("malformado");
    }

    // ─── ModalidadesMinimas ─────────────────────────────────────────────

    [Fact(DisplayName = "ModalidadesMinimas aprova quando todas modalidades presentes")]
    public void ModalidadesMinimas_Aprovada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            CodigosModalidadesPresentes = ["AC", "LbPpi", "LiQ"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new ModalidadesMinimas(["AC", "LbPpi"]));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "ModalidadesMinimas reprova quando falta modalidade e cita ausente")]
    public void ModalidadesMinimas_Reprovada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            CodigosModalidadesPresentes = ["AC", "LbPpi"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new ModalidadesMinimas(["AC", "LbPpi", "LiQ"]));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeFalse();
        resultado.Regras[0].DescricaoHumana.Should().Contain("LiQ");
    }

    [Fact(DisplayName = "ModalidadesMinimas reprova quando lista vazia (malformada)")]
    public void ModalidadesMinimas_Malformada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            CodigosModalidadesPresentes = ["AC"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new ModalidadesMinimas([]));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeFalse();
        resultado.Regras[0].DescricaoHumana.Should().Contain("malformado");
    }

    [Fact(DisplayName = "ModalidadesMinimas reprova quando lista contém só whitespace (malformada)")]
    public void ModalidadesMinimas_SoBranco_Malformada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            CodigosModalidadesPresentes = ["AC"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new ModalidadesMinimas(["  ", "\t", ""]));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeFalse();
        resultado.Regras[0].DescricaoHumana.Should().Contain("malformado");
    }

    // ─── DesempateDeveIncluir ───────────────────────────────────────────

    [Fact(DisplayName = "DesempateDeveIncluir aprova quando critério configurado")]
    public void Desempate_Aprovada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            CriteriosDesempateConfigurados = ["Idoso", "DataNascimento"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new DesempateDeveIncluir("Idoso"));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "DesempateDeveIncluir reprova quando critério ausente")]
    public void Desempate_Reprovada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            CriteriosDesempateConfigurados = ["DataNascimento"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new DesempateDeveIncluir("Idoso"));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeFalse();
        resultado.Regras[0].DescricaoHumana.Should().Contain("Idoso");
    }

    [Fact(DisplayName = "DesempateDeveIncluir reprova quando critério vazio (malformado)")]
    public void Desempate_Malformada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            CriteriosDesempateConfigurados = ["Idoso"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new DesempateDeveIncluir("   "));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeFalse();
        resultado.Regras[0].DescricaoHumana.Should().Contain("malformado");
    }

    // ─── DocumentoObrigatorioParaModalidade ─────────────────────────────

    [Fact(DisplayName = "DocumentoObrigatorioParaModalidade aprova quando par configurado")]
    public void Documento_Aprovada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            DocumentosObrigatorios = [new DocumentoObrigatoriedadeView("LbPpi", "AutoDeclaracao")],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new DocumentoObrigatorioParaModalidade("LbPpi", "AutoDeclaracao"));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "DocumentoObrigatorioParaModalidade reprova quando par ausente")]
    public void Documento_Reprovada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            DocumentosObrigatorios = [new DocumentoObrigatoriedadeView("LbPpi", "AutoDeclaracao")],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new DocumentoObrigatorioParaModalidade("LbQ", "Heteroidentificacao"));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeFalse();
        resultado.Regras[0].DescricaoHumana.Should().Contain("LbQ");
    }

    [Fact(DisplayName = "DocumentoObrigatorioParaModalidade reprova quando modalidade vazia (malformado)")]
    public void Documento_Malformada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            DocumentosObrigatorios = [new DocumentoObrigatoriedadeView("LbPpi", "AutoDeclaracao")],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new DocumentoObrigatorioParaModalidade("   ", "AutoDeclaracao"));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeFalse();
        resultado.Regras[0].DescricaoHumana.Should().Contain("malformado");
    }

    // ─── Smokes das 4 variantes não-core ────────────────────────────────

    [Fact(DisplayName = "BonusObrigatorio aprova quando todas modalidades aplicáveis têm bônus")]
    public void Bonus_Smoke_Aprovada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            ModalidadesComBonus = ["AC", "LbPpi"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new BonusObrigatorio(["AC"]));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "AtendimentoDisponivel aprova quando necessidades cobertas")]
    public void Atendimento_Smoke_Aprovada()
    {
        EditalConformidadeView view = ViewVazia() with
        {
            AtendimentosDisponiveis = ["LibrasInterprete", "ProvaAmpliada"],
        };
        ObrigatoriedadeLegal regra = NovaRegra(new AtendimentoDisponivel(["LibrasInterprete"]));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "ConcorrenciaDuplaObrigatoria aprova quando habilitada no edital")]
    public void ConcorrenciaDupla_Smoke_Aprovada()
    {
        EditalConformidadeView view = ViewVazia() with { ConcorrenciaDuplaHabilitada = true };
        ObrigatoriedadeLegal regra = NovaRegra(new ConcorrenciaDuplaObrigatoria());

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(view, [regra]);

        resultado.Regras[0].Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "Customizado reprova conservadoramente e propaga aviso estruturado")]
    public void Customizado_Smoke_Reprova_E_Avisa()
    {
        using JsonDocument doc = JsonDocument.Parse("{\"campoArbitrario\":\"valor\"}");
        ObrigatoriedadeLegal regra = NovaRegra(new Customizado(doc.RootElement.Clone()));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(
            ViewVazia(),
            [regra]);

        resultado.Regras[0].Aprovada.Should().BeFalse(
            "ADR-0058 §válvula de escape: avaliação manual é exigida para preservar evidência legal");
        resultado.Regras[0].DescricaoHumana.Should().Contain("Customizado");
        resultado.Avisos.Should().Contain(a =>
            a.Contains("Customizado em uso", StringComparison.Ordinal) &&
            a.Contains(regra.RegraCodigo, StringComparison.Ordinal));
    }

    // ─── Round-trip JSON polimórfico (P3 Codex) ─────────────────────────

    [Theory(DisplayName = "PredicadoObrigatoriedade serializa/deserializa polimorficamente preservando $tipo")]
    [MemberData(nameof(VariantesParaRoundTrip))]
    public void RoundTrip_JsonPolimorfico(PredicadoObrigatoriedade original)
    {
        ArgumentNullException.ThrowIfNull(original);
        string json = JsonSerializer.Serialize(original, PredicadoObrigatoriedade.JsonOptions);
        PredicadoObrigatoriedade? voltou = JsonSerializer.Deserialize<PredicadoObrigatoriedade>(
            json,
            PredicadoObrigatoriedade.JsonOptions);

        json.Should().Contain("\"$tipo\":");
        voltou.Should().NotBeNull();
        voltou!.GetType().Should().Be(original.GetType());
    }

    public static TheoryData<PredicadoObrigatoriedade> VariantesParaRoundTrip()
    {
        TheoryData<PredicadoObrigatoriedade> data = new()
        {
            new EtapaObrigatoria("ProvaObjetiva"),
            new ModalidadesMinimas(["AC", "LbPpi"]),
            new DesempateDeveIncluir("Idoso"),
            new DocumentoObrigatorioParaModalidade("LbPpi", "AutoDeclaracao"),
            new BonusObrigatorio(["AC"]),
            new AtendimentoDisponivel(["LibrasInterprete"]),
            new ConcorrenciaDuplaObrigatoria(),
        };
        return data;
    }

    [Fact(DisplayName = "PredicadoObrigatoriedade rejeita JSON com discriminator $tipo desconhecido")]
    public void Deserialize_DiscriminatorDesconhecido_Lanca()
    {
        // CA da Task #491: contrato JSON do System.Text.Json com [JsonPolymorphic]
        // deve rejeitar tipos não registrados via [JsonDerivedType] — proteção
        // contra row corrompida no jsonb (cenário BDD #459 "Predicado JSON
        // malformado quebra deserialização").
        const string jsonComTipoDesconhecido = """{"$tipo":"variantePropostaQueNaoExiste","x":1}""";

        Action act = () => JsonSerializer.Deserialize<PredicadoObrigatoriedade>(
            jsonComTipoDesconhecido,
            PredicadoObrigatoriedade.JsonOptions);

        act.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "PredicadoObrigatoriedade rejeita JSON sem discriminator $tipo")]
    public void Deserialize_SemDiscriminator_Lanca()
    {
        // Reforça a invariante: rows no jsonb sem o campo $tipo (ex.: legado
        // pré-#459 ou corrupção) não devem deserializar para nenhuma variante.
        // STJ levanta NotSupportedException por se tratar de tipo abstrato sem
        // discriminator (vs JsonException para discriminator desconhecido).
        const string jsonSemDiscriminator = """{"tipoEtapaCodigo":"ProvaObjetiva"}""";

        Action act = () => JsonSerializer.Deserialize<PredicadoObrigatoriedade>(
            jsonSemDiscriminator,
            PredicadoObrigatoriedade.JsonOptions);

        act.Should().Throw<NotSupportedException>();
    }

    // ─── Guards de argumento ────────────────────────────────────────────

    [Fact(DisplayName = "Evaluate com view nula lança ArgumentNullException")]
    public void Evaluate_ViewNula_Lanca()
    {
        Action act = () => ValidadorConformidadeEdital.Evaluate(
            (EditalConformidadeView)null!,
            []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "Evaluate com regras nulas lança ArgumentNullException")]
    public void Evaluate_RegrasNulas_Lanca()
    {
        Action act = () => ValidadorConformidadeEdital.Evaluate(ViewVazia(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private static EditalConformidadeView ViewVazia() => new(
        EditalId: Guid.CreateVersion7(),
        CodigosTiposEtapaPresentes: [],
        CodigosModalidadesPresentes: [],
        CriteriosDesempateConfigurados: [],
        DocumentosObrigatorios: [],
        ModalidadesComBonus: [],
        AtendimentosDisponiveis: [],
        ConcorrenciaDuplaHabilitada: false);

    private static ObrigatoriedadeLegal NovaRegra(PredicadoObrigatoriedade predicado)
    {
        Kernel.Results.Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            regraCodigo: "REGRA_TESTE",
            predicado: predicado,
            baseLegal: "Lei 12.711/2012 art.1º",
            descricaoHumana: "Regra de teste",
            portariaInternaCodigo: "Portaria CTIC 2026/01");

        r.IsSuccess.Should().BeTrue();
        return r.Value!;
    }

    [Fact(DisplayName = "Evaluate com Edital agregado projeta para view via From e funciona")]
    public void Evaluate_ComEdital_FuncionaViaProjecao()
    {
        // Cobre o overload público que recebe Edital — confirma que o From
        // extrai etapas/modalidades sem explodir.
        Kernel.Results.Result<NumeroEdital> numero = NumeroEdital.Criar(1, 2026);
        numero.IsSuccess.Should().BeTrue();

        Edital edital = Edital.Criar(numero.Value!, "Edital de teste");
        ObrigatoriedadeLegal regra = NovaRegra(new EtapaObrigatoria("ProvaObjetiva"));

        ResultadoConformidade resultado = ValidadorConformidadeEdital.Evaluate(edital, [regra]);

        // Edital sem etapas → EtapaObrigatoria reprova.
        resultado.Regras[0].Aprovada.Should().BeFalse();
    }
}

/// <summary>
/// Fitness reflectivo: itera por reflection sobre todos os tipos
/// derivados de <see cref="PredicadoObrigatoriedade"/> e verifica que
/// o avaliador responde a cada variante sem cair no catch-all do switch
/// (<see cref="InvalidOperationException"/>). Esse é o gate real de
/// exhaustividade exigido pelo CA-04 da #459 — C# não suporta union
/// fechada nativamente, então a garantia vive aqui.
/// </summary>
public sealed class ValidadorConformidadeEditalExhaustividadeTests
{
    [Fact(DisplayName = "Avaliador responde a TODAS as variantes derivadas de PredicadoObrigatoriedade")]
    public void TodasVariantesAvaliadasSemFallback()
    {
        Type baseType = typeof(PredicadoObrigatoriedade);
        Type[] derivados = baseType.Assembly
            .GetTypes()
            .Where(t => baseType.IsAssignableFrom(t) && t != baseType && !t.IsAbstract)
            .ToArray();

        derivados.Should().NotBeEmpty("PredicadoObrigatoriedade deve ter variantes concretas registradas");

        EditalConformidadeView view = new(
            EditalId: Guid.CreateVersion7(),
            CodigosTiposEtapaPresentes: ["ProvaObjetiva"],
            CodigosModalidadesPresentes: ["AC"],
            CriteriosDesempateConfigurados: ["Idoso"],
            DocumentosObrigatorios: [new DocumentoObrigatoriedadeView("AC", "RG")],
            ModalidadesComBonus: ["AC"],
            AtendimentosDisponiveis: ["LibrasInterprete"],
            ConcorrenciaDuplaHabilitada: true);

        foreach (Type derivado in derivados)
        {
            PredicadoObrigatoriedade predicado = InstanciarVariante(derivado);
            Kernel.Results.Result<ObrigatoriedadeLegal> regra = ObrigatoriedadeLegal.Criar(
                regraCodigo: $"FITNESS_{derivado.Name}",
                predicado: predicado,
                baseLegal: "Lei 12.711/2012",
                descricaoHumana: "Fitness");
            regra.IsSuccess.Should().BeTrue();

            Action act = () => ValidadorConformidadeEdital.Evaluate(view, [regra.Value!]);

            act.Should().NotThrow(
                $"variante {derivado.Name} precisa ter case explícito em ValidadorConformidadeEdital.Avaliar");
        }
    }

    private static PredicadoObrigatoriedade InstanciarVariante(Type tipo)
    {
        if (tipo == typeof(EtapaObrigatoria))
            return new EtapaObrigatoria("ProvaObjetiva");
        if (tipo == typeof(ModalidadesMinimas))
            return new ModalidadesMinimas(["AC"]);
        if (tipo == typeof(DesempateDeveIncluir))
            return new DesempateDeveIncluir("Idoso");
        if (tipo == typeof(DocumentoObrigatorioParaModalidade))
            return new DocumentoObrigatorioParaModalidade("AC", "RG");
        if (tipo == typeof(BonusObrigatorio))
            return new BonusObrigatorio(["AC"]);
        if (tipo == typeof(AtendimentoDisponivel))
            return new AtendimentoDisponivel(["LibrasInterprete"]);
        if (tipo == typeof(ConcorrenciaDuplaObrigatoria))
            return new ConcorrenciaDuplaObrigatoria();
        if (tipo == typeof(Customizado))
        {
            using JsonDocument doc = JsonDocument.Parse("{}");
            return new Customizado(doc.RootElement.Clone());
        }

        throw new NotSupportedException(
            $"Adicione factory para nova variante {tipo.Name} em ValidadorConformidadeEditalExhaustividadeTests.InstanciarVariante "
            + "(esse erro é intencional — confirma que o fitness sabe instanciar cada derivado).");
    }
}
