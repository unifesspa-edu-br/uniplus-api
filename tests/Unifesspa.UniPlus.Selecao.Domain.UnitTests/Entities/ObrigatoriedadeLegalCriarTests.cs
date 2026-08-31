namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Invariantes da forma plena de <see cref="ObrigatoriedadeLegal"/> (Story
/// #460). Cobre os critérios CA-01 (campos) e CA-05 (hash recomputado pela
/// factory).
/// </summary>
public sealed class ObrigatoriedadeLegalCriarTests
{
    private static readonly PredicadoObrigatoriedade PredicadoBase =
        new EtapaObrigatoria("ProvaObjetiva");

    [Fact(DisplayName = "Criar regra universal global popula campos e computa hash")]
    public void Criar_RegraUniversalGlobal_OK()
    {
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: PredicadoBase,
            descricaoHumana: "Edital deve incluir etapa de Prova Objetiva.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1));

        r.IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = r.Value!;
        regra.TipoProcessoCodigo.Should().Be("*");
        regra.Categoria.Should().Be(CategoriaObrigatoriedade.Etapa);
        regra.RegraCodigo.Should().Be("ETAPA_OBRIGATORIA");
        HashCanonicalComputer.IsValidHashShape(regra.Hash).Should().BeTrue();
    }

    [Theory(DisplayName = "TipoProcessoCodigo estruturalmente válido é aceito; a atividade é validada pelo catálogo no handler")]
    [InlineData("*")]
    [InlineData("SiSU")]
    [InlineData("PSIQ")]
    [InlineData("PSECampo")]
    [InlineData("PSVR")]
    [InlineData("TransferenciaInterna")]
    [InlineData("TransferenciaExterna")]
    [InlineData("PortadorDiploma")]
    [InlineData("Reopcao")]
    public void Criar_TipoProcessoCodigoUniversalOuValido_Aceita(string tipoProcessoCodigo)
    {
        Result<ObrigatoriedadeLegal> resultado = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo,
            CategoriaObrigatoriedade.Outros,
            "TIPO_VALIDO",
            PredicadoBase,
            "Descrição",
            "Lei",
            new DateOnly(2026, 1, 1));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.TipoProcessoCodigo.Should().Be(tipoProcessoCodigo);
    }

    [Theory(DisplayName = "TipoProcessoCodigo estruturalmente válido não depende de enum fechado")]
    [InlineData("SISU_ANTIGO")]
    [InlineData("sisu")]
    [InlineData("Nenhum")]
    public void Criar_TipoProcessoCodigoEstruturalmenteValido_Aceita(string tipoProcessoCodigo)
    {
        Result<ObrigatoriedadeLegal> resultado = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo,
            CategoriaObrigatoriedade.Outros,
            "TIPO_INVALIDO",
            PredicadoBase,
            "Descrição",
            "Lei",
            new DateOnly(2026, 1, 1));

        resultado.IsSuccess.Should().BeTrue(
            "o domínio só conhece a estrutura do código; a existência e a atividade são verificadas pelo handler " +
            "contra o catálogo de Configuração");
        resultado.Value!.TipoProcessoCodigo.Should().Be(tipoProcessoCodigo);
    }

    [Fact(DisplayName = "VigenciaFim igual a VigenciaInicio é inválida")]
    public void Criar_VigenciaFimNaoPosterior_Falha()
    {
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: "*",
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: "X",
            predicado: PredicadoBase,
            descricaoHumana: "x",
            baseLegal: "Lei 12.711/2012",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: new DateOnly(2026, 1, 1));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("ObrigatoriedadeLegal.VigenciaInvalida");
    }

    [Fact(DisplayName = "Predicado null retorna Result.Failure (não throw) — preserva mapping de DomainError")]
    public void Criar_PredicadoNull_RetornaFailure()
    {
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: "*",
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: "X",
            predicado: null!,
            descricaoHumana: "x",
            baseLegal: "Lei",
            vigenciaInicio: new DateOnly(2026, 1, 1));

        r.IsFailure.Should().BeTrue(
            "factory deve devolver Result.Failure por consistência com os outros campos obrigatórios — "
            + "ArgumentNullException viraria HTTP 500 no pipeline em vez do 422 esperado");
        r.Error!.Code.Should().Be("ObrigatoriedadeLegal.PredicadoObrigatorio");
    }

    [Fact(DisplayName = "Atualizar com predicado null retorna Result.Failure (não throw)")]
    public void Atualizar_PredicadoNull_RetornaFailure()
    {
        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "X",
            predicado: PredicadoBase,
            descricaoHumana: "x",
            baseLegal: "Lei",
            vigenciaInicio: new DateOnly(2026, 1, 1)).Value!;

        Kernel.Results.Result r = regra.Atualizar(
            tipoProcessoCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "X",
            predicado: null!,
            descricaoHumana: "x",
            baseLegal: "Lei",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: null,
            atoNormativoUrl: null,
            portariaInternaCodigo: null);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("ObrigatoriedadeLegal.PredicadoObrigatorio");
    }

    [Fact(DisplayName = "Categoria Nenhuma (default sentinel) é rejeitada")]
    public void Criar_CategoriaNenhuma_Falha()
    {
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: "*",
            categoria: CategoriaObrigatoriedade.Nenhuma,
            regraCodigo: "X",
            predicado: PredicadoBase,
            descricaoHumana: "x",
            baseLegal: "Lei",
            vigenciaInicio: new DateOnly(2026, 1, 1));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("ObrigatoriedadeLegal.CategoriaInvalida");
    }

    [Theory(DisplayName = "Campos obrigatórios vazios são rejeitados com erro específico")]
    [InlineData("", "Lei", "Desc", "ObrigatoriedadeLegal.RegraCodigoObrigatorio")]
    [InlineData("X", "", "Desc", "ObrigatoriedadeLegal.BaseLegalObrigatoria")]
    [InlineData("X", "Lei", "", "ObrigatoriedadeLegal.DescricaoHumanaObrigatoria")]
    public void Criar_ObrigatoriosVazios_Falha(string regra, string baseLegal, string descricao, string expectedCode)
    {
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: "*",
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: regra,
            predicado: PredicadoBase,
            descricaoHumana: descricao,
            baseLegal: baseLegal,
            vigenciaInicio: new DateOnly(2026, 1, 1));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(expectedCode);
    }

    [Fact(DisplayName = "Atualizar recomputa o hash quando campo semântico muda")]
    public void Atualizar_AlteraBaseLegal_HashMuda()
    {
        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: PredicadoBase,
            descricaoHumana: "Edital deve incluir etapa de Prova Objetiva.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1)).Value!;

        string hashAntes = regra.Hash;

        Result r = regra.Atualizar(
            tipoProcessoCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: PredicadoBase,
            descricaoHumana: "Edital deve incluir etapa de Prova Objetiva.",
            baseLegal: "Lei 14.723/2023 art.2º",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: null,
            atoNormativoUrl: null,
            portariaInternaCodigo: null);

        r.IsSuccess.Should().BeTrue();
        regra.Hash.Should().NotBe(hashAntes);
        regra.BaseLegal.Should().Be("Lei 14.723/2023 art.2º");
    }

    [Fact(DisplayName = "Atualizar é full-replace — passar null em opcional limpa o estado anterior")]
    public void Atualizar_FullReplace_LimpaOpcionaisNaoPassados()
    {
        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_FULL_REPLACE",
            predicado: PredicadoBase,
            descricaoHumana: "Regra com opcionais preenchidos.",
            baseLegal: "Lei 12.711/2012",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: new DateOnly(2027, 1, 1),
            atoNormativoUrl: "https://www.planalto.gov.br/lei",
            portariaInternaCodigo: "PORT/2026/01").Value!;

        regra.AtoNormativoUrl.Should().Be("https://www.planalto.gov.br/lei");
        regra.PortariaInternaCodigo.Should().Be("PORT/2026/01");

        // Caller que NÃO repassa os opcionais aceita semântica full-replace:
        // o estado anterior dos opcionais é apagado.
        Result r = regra.Atualizar(
            tipoProcessoCodigo: regra.TipoProcessoCodigo,
            categoria: regra.Categoria,
            regraCodigo: regra.RegraCodigo,
            predicado: regra.Predicado,
            descricaoHumana: regra.DescricaoHumana,
            baseLegal: regra.BaseLegal,
            vigenciaInicio: regra.VigenciaInicio,
            vigenciaFim: null,
            atoNormativoUrl: null,
            portariaInternaCodigo: null);

        r.IsSuccess.Should().BeTrue();
        regra.VigenciaFim.Should().BeNull();
        regra.AtoNormativoUrl.Should().BeNull();
        regra.PortariaInternaCodigo.Should().BeNull();
    }

    [Fact(DisplayName = "Factory de retrocompatibilidade aplica defaults pragmáticos")]
    public void Criar_RetroCompat_AplicaDefaults()
    {
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            regraCodigo: "REGRA_LEGADA",
            predicado: PredicadoBase,
            baseLegal: "Lei 12.711/2012",
            descricaoHumana: "Regra retrocompatível.",
            portariaInternaCodigo: null,
            clock: TimeProvider.System);

        r.IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = r.Value!;
        regra.TipoProcessoCodigo.Should().Be(ObrigatoriedadeLegal.TipoProcessoUniversal);
        regra.Categoria.Should().Be(CategoriaObrigatoriedade.Outros);
        regra.VigenciaFim.Should().BeNull();
    }

    [Theory(DisplayName = "Predicado com código em branco é recusado pela factory, sem chegar a persistir")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_PredicadoComCodigoEmBranco_Falha(string? codigo)
    {
        // Código em branco nunca casa com o valor congelado no processo seletivo, que a
        // avaliação de conformidade compara por igualdade ordinal: a cláusula legal existiria
        // como cumprida sem exigir nada de ninguém.
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_SEM_CODIGO",
            predicado: new EtapaObrigatoria(codigo!),
            descricaoHumana: "Edital deve incluir a etapa.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("ObrigatoriedadeLegal.PredicadoComCodigoEmBranco");
    }

    [Fact(DisplayName = "Documento por modalidade sem o código do tipo de documento é recusado")]
    public void Criar_DocumentoParaModalidadeSemTipoDocumento_Falha()
    {
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: "DOCUMENTO_SEM_TIPO",
            predicado: new DocumentoObrigatorioParaModalidade("LB_PPI", "  "),
            descricaoHumana: "Modalidade exige o documento.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("ObrigatoriedadeLegal.PredicadoComCodigoEmBranco");
    }

    [Fact(DisplayName = "Atualizar para modalidades mínimas com código em branco na lista é recusado")]
    public void Atualizar_ModalidadesMinimasComCodigoEmBranco_Falha()
    {
        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: "MODALIDADES_MINIMAS",
            predicado: new ModalidadesMinimas(["AC"]),
            descricaoHumana: "Edital deve ofertar as modalidades.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1)).Value!;

        Result r = regra.Atualizar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: "MODALIDADES_MINIMAS",
            predicado: new ModalidadesMinimas(["AC", "   "]),
            descricaoHumana: "Edital deve ofertar as modalidades.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: null,
            atoNormativoUrl: null,
            portariaInternaCodigo: null);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("ObrigatoriedadeLegal.ModalidadesMinimasVazia");
        regra.Predicado.Should().BeOfType<ModalidadesMinimas>()
            .Which.Codigos.Should().ContainSingle("a recusa acontece antes de aplicar o payload");
    }
}
