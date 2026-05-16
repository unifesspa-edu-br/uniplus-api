namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Collections.Generic;

using AwesomeAssertions;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Invariantes da forma plena de <see cref="ObrigatoriedadeLegal"/> (Story
/// #460). Cobre os critérios CA-01 (campos), CA-05 (hash recomputado pela
/// factory) e a Invariante 1 do ADR-0057 (Proprietario ∈ AreasDeInteresse).
/// </summary>
public sealed class ObrigatoriedadeLegalCriarTests
{
    private static readonly PredicadoObrigatoriedade PredicadoBase =
        new EtapaObrigatoria("ProvaObjetiva");

    [Fact(DisplayName = "Criar regra universal global popula campos e computa hash")]
    public void Criar_RegraUniversalGlobal_OK()
    {
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoEditalCodigo: ObrigatoriedadeLegal.TipoEditalUniversal,
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: PredicadoBase,
            descricaoHumana: "Edital deve incluir etapa de Prova Objetiva.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1));

        r.IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = r.Value!;
        regra.TipoEditalCodigo.Should().Be("*");
        regra.Categoria.Should().Be(CategoriaObrigatoriedade.Etapa);
        regra.RegraCodigo.Should().Be("ETAPA_OBRIGATORIA");
        regra.Proprietario.Should().BeNull();
        regra.AreasDeInteresse.Should().BeEmpty();
        HashCanonicalComputer.IsValidHashShape(regra.Hash).Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com governance respeita Invariante 1 (Proprietario ∈ AreasDeInteresse)")]
    public void Criar_ComGovernance_OK()
    {
        AreaCodigo ceps = AreaCodigo.From("CEPS").Value;
        AreaCodigo proeg = AreaCodigo.From("PROEG").Value;

        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoEditalCodigo: ObrigatoriedadeLegal.TipoEditalUniversal,
            categoria: CategoriaObrigatoriedade.Modalidade,
            regraCodigo: "MODALIDADES_MINIMAS_CEPS",
            predicado: new ModalidadesMinimas(new[] { "AC", "LbPpi" }),
            descricaoHumana: "Modalidades mínimas para PS CEPS.",
            baseLegal: "Resolução Unifesspa 414/2020",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            proprietario: ceps,
            areasDeInteresse: new HashSet<AreaCodigo> { ceps, proeg });

        r.IsSuccess.Should().BeTrue();
        r.Value!.Proprietario.Should().Be(ceps);
        r.Value!.AreasDeInteresse.Should().BeEquivalentTo(new[] { ceps, proeg });
    }

    [Fact(DisplayName = "Proprietario fora de AreasDeInteresse falha com erro do ADR-0057")]
    public void Criar_ProprietarioForaDasAreas_Falha()
    {
        AreaCodigo ceps = AreaCodigo.From("CEPS").Value;
        AreaCodigo crca = AreaCodigo.From("CRCA").Value;

        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: "X",
            predicado: new ConcorrenciaDuplaObrigatoria(),
            descricaoHumana: "x",
            baseLegal: "Lei 12.711/2012",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            proprietario: ceps,
            areasDeInteresse: new HashSet<AreaCodigo> { crca });

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("ObrigatoriedadeLegal.ProprietarioForaDeAreasDeInteresse");
    }

    [Fact(DisplayName = "AreasDeInteresse sem Proprietario falha")]
    public void Criar_AreasSemProprietario_Falha()
    {
        AreaCodigo ceps = AreaCodigo.From("CEPS").Value;

        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: "X",
            predicado: new ConcorrenciaDuplaObrigatoria(),
            descricaoHumana: "x",
            baseLegal: "Lei 12.711/2012",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            proprietario: null,
            areasDeInteresse: new HashSet<AreaCodigo> { ceps });

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("ObrigatoriedadeLegal.ProprietarioObrigatorioComAreas");
    }

    [Fact(DisplayName = "VigenciaFim igual a VigenciaInicio é inválida")]
    public void Criar_VigenciaFimNaoPosterior_Falha()
    {
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoEditalCodigo: "*",
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
            tipoEditalCodigo: "*",
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
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "X",
            predicado: PredicadoBase,
            descricaoHumana: "x",
            baseLegal: "Lei",
            vigenciaInicio: new DateOnly(2026, 1, 1)).Value!;

        Kernel.Results.Result r = regra.Atualizar(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "X",
            predicado: null!,
            descricaoHumana: "x",
            baseLegal: "Lei",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: null,
            atoNormativoUrl: null,
            portariaInternaCodigo: null,
            proprietario: null,
            areasDeInteresse: null);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("ObrigatoriedadeLegal.PredicadoObrigatorio");
    }

    [Fact(DisplayName = "Categoria Nenhuma (default sentinel) é rejeitada")]
    public void Criar_CategoriaNenhuma_Falha()
    {
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            tipoEditalCodigo: "*",
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
            tipoEditalCodigo: "*",
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
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: PredicadoBase,
            descricaoHumana: "Edital deve incluir etapa de Prova Objetiva.",
            baseLegal: "Lei 12.711/2012 art.1º",
            vigenciaInicio: new DateOnly(2026, 1, 1)).Value!;

        string hashAntes = regra.Hash;

        Result r = regra.Atualizar(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_OBRIGATORIA",
            predicado: PredicadoBase,
            descricaoHumana: "Edital deve incluir etapa de Prova Objetiva.",
            baseLegal: "Lei 14.723/2023 art.2º",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: null,
            atoNormativoUrl: null,
            portariaInternaCodigo: null,
            proprietario: null,
            areasDeInteresse: null);

        r.IsSuccess.Should().BeTrue();
        regra.Hash.Should().NotBe(hashAntes);
        regra.BaseLegal.Should().Be("Lei 14.723/2023 art.2º");
    }

    [Fact(DisplayName = "Atualizar é full-replace — passar null em opcional limpa o estado anterior")]
    public void Atualizar_FullReplace_LimpaOpcionaisNaoPassados()
    {
        AreaCodigo ceps = AreaCodigo.From("CEPS").Value;

        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            tipoEditalCodigo: "*",
            categoria: CategoriaObrigatoriedade.Etapa,
            regraCodigo: "ETAPA_FULL_REPLACE",
            predicado: PredicadoBase,
            descricaoHumana: "Regra com opcionais preenchidos.",
            baseLegal: "Lei 12.711/2012",
            vigenciaInicio: new DateOnly(2026, 1, 1),
            vigenciaFim: new DateOnly(2027, 1, 1),
            atoNormativoUrl: "https://www.planalto.gov.br/lei",
            portariaInternaCodigo: "PORT/2026/01",
            proprietario: ceps,
            areasDeInteresse: new HashSet<AreaCodigo> { ceps }).Value!;

        regra.AtoNormativoUrl.Should().Be("https://www.planalto.gov.br/lei");
        regra.PortariaInternaCodigo.Should().Be("PORT/2026/01");
        regra.Proprietario.Should().Be(ceps);

        // Caller que NÃO repassa os opcionais aceita semântica full-replace:
        // o estado anterior dos opcionais é apagado, e o invariante de
        // governance proíbe AreasDeInteresse vazio com Proprietario setado.
        Result r = regra.Atualizar(
            tipoEditalCodigo: regra.TipoEditalCodigo,
            categoria: regra.Categoria,
            regraCodigo: regra.RegraCodigo,
            predicado: regra.Predicado,
            descricaoHumana: regra.DescricaoHumana,
            baseLegal: regra.BaseLegal,
            vigenciaInicio: regra.VigenciaInicio,
            vigenciaFim: null,
            atoNormativoUrl: null,
            portariaInternaCodigo: null,
            proprietario: null,
            areasDeInteresse: null);

        r.IsSuccess.Should().BeTrue();
        regra.VigenciaFim.Should().BeNull();
        regra.AtoNormativoUrl.Should().BeNull();
        regra.PortariaInternaCodigo.Should().BeNull();
        regra.Proprietario.Should().BeNull();
        regra.AreasDeInteresse.Should().BeEmpty();
    }

    [Fact(DisplayName = "Factory de retrocompatibilidade aplica defaults pragmáticos")]
    public void Criar_RetroCompat_AplicaDefaults()
    {
        Result<ObrigatoriedadeLegal> r = ObrigatoriedadeLegal.Criar(
            regraCodigo: "REGRA_LEGADA",
            predicado: PredicadoBase,
            baseLegal: "Lei 12.711/2012",
            descricaoHumana: "Regra retrocompatível.");

        r.IsSuccess.Should().BeTrue();
        ObrigatoriedadeLegal regra = r.Value!;
        regra.TipoEditalCodigo.Should().Be(ObrigatoriedadeLegal.TipoEditalUniversal);
        regra.Categoria.Should().Be(CategoriaObrigatoriedade.Outros);
        regra.VigenciaFim.Should().BeNull();
        regra.Proprietario.Should().BeNull();
        regra.AreasDeInteresse.Should().BeEmpty();
    }
}
