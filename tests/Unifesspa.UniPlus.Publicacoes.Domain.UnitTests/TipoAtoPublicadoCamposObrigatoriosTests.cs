namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os atributos do tipo de ato e o início da vigência são obrigatórios: ausentes (ou nulos, que o domínio
/// não distingue), são recusados como "obrigatório", acumulados com as demais recusas, em vez de virarem
/// <c>false</c> ou o ano 1 em silêncio.
/// </summary>
public sealed class TipoAtoPublicadoCamposObrigatoriosTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    private static readonly string[] CamposEmOrdemDoContrato =
    [
        "congelaConfiguracao", "unicoPorObjeto", "efeitoIrreversivel", "ehResultado",
        "publicavelComoAvulso", "exigeAtoCorrigido", "vigenciaInicio"
    ];

    [Theory(DisplayName = "Campo obrigatório ausente é recusado como obrigatório, nomeando só ele")]
    [InlineData("congelaConfiguracao")]
    [InlineData("unicoPorObjeto")]
    [InlineData("efeitoIrreversivel")]
    [InlineData("ehResultado")]
    [InlineData("publicavelComoAvulso")]
    [InlineData("exigeAtoCorrigido")]
    [InlineData("vigenciaInicio")]
    public void Criar_SemUmCampoObrigatorio_RecusaSoEsseCampo(string campo)
    {
        Result<TipoAtoPublicado> resultado = SemOCampo(new TipoAtoPublicadoDeTeste(), campo).Criar();

        resultado.IsFailure.Should().BeTrue();
        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be(campo);
        recusa.Error.Code.Should().Be(CodigosDeValidacao.Obrigatorio);
        recusa.Error.Message.Should().StartWith("Informe ");
    }

    [Theory(DisplayName = "Atualizar sem um campo obrigatório recusa e não muta a entidade")]
    [InlineData("publicavelComoAvulso")]
    [InlineData("vigenciaInicio")]
    public void Atualizar_SemUmCampoObrigatorio_RecusaENaoMuta(string campo)
    {
        TipoAtoPublicadoDeTeste declaracao = new() { PublicavelComoAvulso = true, ExigeAtoCorrigido = true };
        TipoAtoPublicado tipo = declaracao.Criar().Value!;

        Result resultado = SemOCampo(declaracao with { Nome = "Outro nome" }, campo).Atualizar(tipo);

        resultado.Errors.Should().ContainSingle(e => e.Field == campo && e.Error.Code == CodigosDeValidacao.Obrigatorio);
        tipo.Nome.Should().Be("Edital de abertura");
        tipo.PublicavelComoAvulso.Should().BeTrue();
        tipo.VigenciaInicio.Should().Be(Inicio);
    }

    [Fact(DisplayName = "Tudo ausente acumula as recusas na ordem: código, nome, os seis atributos, início da vigência")]
    public void Criar_ComTudoAusente_AcumulaNaOrdemDoContrato()
    {
        TipoAtoPublicadoDeTeste vazio = new()
        {
            Codigo = null,
            Nome = null,
            CongelaConfiguracao = null,
            UnicoPorObjeto = null,
            EfeitoIrreversivel = null,
            EhResultado = null,
            PublicavelComoAvulso = null,
            ExigeAtoCorrigido = null,
            VigenciaInicio = null
        };

        Result<TipoAtoPublicado> resultado = vazio.Criar();

        resultado.Errors.Select(e => e.Field).Should().Equal(["codigo", "nome", .. CamposEmOrdemDoContrato]);
        resultado.Errors.Where(e => e.Field is not ("codigo" or "nome"))
            .Should().OnlyContain(e => e.Error.Code == CodigosDeValidacao.Obrigatorio);
    }

    [Fact(DisplayName = "A recusa do campo ausente acumula com a de base legal longa, depois do início da vigência")]
    public void Criar_CampoAusenteEBaseLegalLonga_AcumulaNessaOrdem()
    {
        TipoAtoPublicadoDeTeste declaracao = new() { Nome = "E", VigenciaInicio = null, BaseLegal = new string('x', 501) };

        Result<TipoAtoPublicado> resultado = declaracao.Criar();

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("nome", TipoAtoPublicadoErrorCodes.NomeTamanho),
            ("vigenciaInicio", CodigosDeValidacao.Obrigatorio),
            ("baseLegal", TipoAtoPublicadoErrorCodes.BaseLegalTamanho));
    }

    [Fact(DisplayName = "Sem o início da vigência, o fim não é comparado com ele")]
    public void Criar_SemInicioComFimDeclarado_NaoAcusaFimAnteriorAoInicio()
    {
        TipoAtoPublicadoDeTeste declaracao = new() { VigenciaInicio = null, VigenciaFim = Inicio.AddDays(-30) };

        Result<TipoAtoPublicado> resultado = declaracao.Criar();

        resultado.Errors.Should().ContainSingle(e => e.Field == "vigenciaInicio");
    }

    [Theory(DisplayName = "Resultado com efeito irreversível só é recusado quando os dois atributos foram informados")]
    [InlineData(null, true, false)]
    [InlineData(true, null, false)]
    [InlineData(true, true, true)]
    public void Criar_ResultadoIrreversivel_SoQuandoAmbosInformados(bool? ehResultado, bool? irreversivel, bool recusaTipo)
    {
        TipoAtoPublicadoDeTeste declaracao = new() { EhResultado = ehResultado, EfeitoIrreversivel = irreversivel };

        Result<TipoAtoPublicado> resultado = declaracao.Criar();

        resultado.Errors.Any(e => e.Error.Code == TipoAtoPublicadoErrorCodes.ResultadoComEfeitoIrreversivel)
            .Should().Be(recusaTipo);
        resultado.Errors.Count(e => e.Error.Code == CodigosDeValidacao.Obrigatorio).Should().Be(recusaTipo ? 0 : 1);
    }

    [Fact(DisplayName = "Atributo informado como falso é aceito: falso é resposta, ausente é que falta")]
    public void Criar_ComAtributosFalsos_Sucesso()
    {
        TipoAtoPublicado tipo = new TipoAtoPublicadoDeTeste().Criar().Value!;

        tipo.CongelaConfiguracao.Should().BeFalse();
        tipo.ExigeAtoCorrigido.Should().BeFalse();
        tipo.VigenciaInicio.Should().Be(Inicio);
    }

    private static TipoAtoPublicadoDeTeste SemOCampo(TipoAtoPublicadoDeTeste declaracao, string campo) => campo switch
    {
        "congelaConfiguracao" => declaracao with { CongelaConfiguracao = null },
        "unicoPorObjeto" => declaracao with { UnicoPorObjeto = null },
        "efeitoIrreversivel" => declaracao with { EfeitoIrreversivel = null },
        "ehResultado" => declaracao with { EhResultado = null },
        "publicavelComoAvulso" => declaracao with { PublicavelComoAvulso = null },
        "exigeAtoCorrigido" => declaracao with { ExigeAtoCorrigido = null },
        "vigenciaInicio" => declaracao with { VigenciaInicio = null },
        _ => throw new ArgumentOutOfRangeException(nameof(campo), campo, "Campo desconhecido."),
    };
}
