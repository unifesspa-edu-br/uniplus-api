namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.ConsultaAdministrativaDeTeste;

/// <summary>
/// O segundo nível da consulta administrativa da inscrição (ADR-0153): cada dado sai pela permissão da classe dele, o
/// identificador só mascarado e revelável a quem também pode revelar, e só a decisão permitida é registrada.
/// </summary>
public sealed class ObterInscricaoAdministrativaProjecaoDosDadosTests
{
    [Fact(DisplayName = "Kevin com dados pessoais e identificadores e sem sensíveis recebe nome e CPF mascarado, sem o tipo de deficiência e sem indício de omissão")]
    public async Task DadoSensivel_OmitidoParaQuemNaoTemAPermissao()
    {
        DecisoesDeAcessoDeTeste kevin = new(
            "kevin.carvalho", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);

        InscricaoAdministrativaDto dto = await ConsultarDto(DeFernanda(), kevin);

        ItensDe(dto).Select(static i => i.FatoCodigo).Should().BeEquivalentTo(["CPF", "NOME"]);
        ItemDe(dto, "CPF").Valor!.Value.GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        JsonSerializer.Serialize(dto).Should().NotContain("BAIXA_VISAO").And.NotContain("TIPO_DEFICIENCIA");
    }

    [Fact(DisplayName = "Registro só da decisão permitida: Kevin tem o acesso e a classe pessoal registrados, e nenhuma negativa de dado sensível")]
    public async Task Registro_SoDaDecisaoPermitida()
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda();
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        await Consultar(cenario, kevin);

        await kevin.RegistrouAsPermitidasAsync(CodigoPessoais);
        await kevin.NaoRegistrouAClasseAsync(CodigoSensiveis);
        await kevin.ExigiuOAcessoAsync(cenario.InscricaoId, CodigoAcesso);
    }

    [Fact(DisplayName = "Leandro com dados públicos e documentos públicos tem as duas decisões registradas, ainda que sejam classes abertas")]
    public async Task Registro_ClassesPublicas_SaoRegistradas()
    {
        DecisoesDeAcessoDeTeste leandro = new(
            "leandro.viana", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DocumentosPublicosConsultar);

        await Consultar(DeFernanda(), leandro);

        await leandro.RegistrouAsPermitidasAsync(CodigoDocumentosPublicos, CodigoPublicos);
    }

    [Theory(DisplayName = "O item do identificador depende de consultar o mascarado e de poder revelar: valor e podeRevelar de cada combinação")]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    public async Task Identificador_PorConsultarERevelar(bool consultar, bool revelar, bool comValor, bool podeRevelar)
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(CpfDeErick)
            .Fato("PASSAPORTE_NUMERO", "IDENTIFICADOR", TipoRenderizacao.Texto, Texto("FX123456"));
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", Permissoes(consultar, revelar));

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, diogo);

        ItemDeRespostaDto passaporte = ItensDe(dto).Single();
        passaporte.Componente.Should().Be(ComponentesDeItem.Identificador);
        passaporte.Classificacao.Should().Be(ClassificacaoDeProtecao.Identificador);
        passaporte.PodeRevelar.Should().Be(podeRevelar);
        passaporte.Valor.HasValue.Should().Be(comValor);
        passaporte.Valor?.GetString().Should().Be(comValor ? ItensDaInscricao.MascaraTotal : null);
        JsonSerializer.Serialize(dto).Should().NotContain("FX123456");
    }

    [Fact(DisplayName = "Isabella com o identificador mascarado e sem revelar recebe o CPF de Fernanda com o componente cpf, mascarado, e podeRevelar falso")]
    public async Task Cpf_SemACapacidadeDeRevelar()
    {
        DecisoesDeAcessoDeTeste isabella = new("isabella.nunes", Permissoes(consultar: true, revelar: false));

        InscricaoAdministrativaDto dto = await ConsultarDto(DeFernanda(), isabella);

        ItemDeRespostaDto cpf = ItemDe(dto, "CPF");
        cpf.Componente.Should().Be(ComponentesDeItem.Cpf);
        cpf.Valor!.Value.GetString().Should().Be("***.598.363-**");
        cpf.PodeRevelar.Should().BeFalse();
    }

    [Fact(DisplayName = "CPF reclassificado como pessoal continua mascarado para quem consulta dados pessoais, sem a opção de revelar")]
    public async Task CpfReclassificadoComoPessoal_SaiMascarado()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Fato("CPF", "PESSOAL", TipoRenderizacao.Texto, Texto(PersonasDeTeste.FernandaDrumond.Valor), ordem: 0, formato: "CPF");
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, kevin);

        ItemDe(dto, "CPF").Valor!.Value.GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        ItemDe(dto, "CPF").PodeRevelar.Should().BeFalse();
    }

    [Theory(DisplayName = "Fernanda, que usa o nome social Ariel Drumond: o nome social é pessoal: sem dados pessoais o detalhe não o traz, e com eles traz")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NomeSocialPessoal_SaiSoComDadosPessoais(bool comDadosPessoais)
    {
        InscricaoAdministrativaDeTeste cenario = DeFernanda()
            .Fato("NOME_SOCIAL", "PESSOAL", TipoRenderizacao.Texto, Texto("Ariel Drumond"), ordem: 3);
        DecisoesDeAcessoDeTeste persona = comDadosPessoais
            ? new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DadosPessoaisConsultar)
            : new("leandro.viana", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, persona);

        ItensDe(dto).Where(static i => i.FatoCodigo == "NOME_SOCIAL").Select(static i => i.Valor!.Value.GetString()!)
            .Should().Equal(comDadosPessoais ? ["Ariel Drumond"] : []);
    }

    [Fact(DisplayName = "Sem consultar nem revelar o item do identificador é omitido")]
    public async Task Identificador_SemConsultarNemRevelar_Omitido()
    {
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", Permissoes(consultar: false, revelar: false));

        InscricaoAdministrativaDto dto = await ConsultarDto(DeFernanda(), kevin);

        ItensDe(dto).Select(static i => i.FatoCodigo).Should().Equal("NOME");
        JsonSerializer.Serialize(dto).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor).And.NotContain("***.598.363-**");
    }

    [Fact(DisplayName = "Só revelar, sem o identificador mascarado: o item é omitido, nada do identificador é registrado e a revelação não é pedida na consulta")]
    public async Task Identificador_SoRevelar_OmiteOItemENaoGravaRegistroDeIdentificador()
    {
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", Permissoes(consultar: false, revelar: true));

        InscricaoAdministrativaDto dto = await ConsultarDto(DeFernanda(), diogo);

        ItensDe(dto).Should().NotContain(static i => i.FatoCodigo == "CPF");
        await diogo.NaoRegistrouAClasseAsync(CodigoIdentificadores);
        await diogo.NaoExigiuAsync(CodigoRevelar);
    }

    [Fact(DisplayName = "Consultar o identificador mascarado publica a decisão do par (dado, identificador)")]
    public async Task Identificador_Consultar_PublicaADecisao()
    {
        DecisoesDeAcessoDeTeste isabella = new("isabella.nunes", Permissoes(consultar: true, revelar: false));

        await Consultar(DeFernanda(), isabella);

        await isabella.RegistrouAClasseAsync(CodigoIdentificadores);
    }

    [Theory(DisplayName = "A resposta do identificador ausente ou vazia não gera item, nem para quem pode revelar")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Identificador_RespostaVazia_SemItem(string resposta)
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(CpfDeErick)
            .Fato("PASSAPORTE_NUMERO", "IDENTIFICADOR", TipoRenderizacao.Texto, Texto(resposta));
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", Permissoes(consultar: true, revelar: true));

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, diogo);

        dto.Secoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Derivado sensível sem base legal congelada não sai, mesmo para quem tem a permissão de dados sensíveis")]
    public async Task Sensivel_SemBaseLegal_NaoSai()
    {
        InscricaoAdministrativaDeTeste cenario = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond)
            .Derivado("COM_BASE", "Com base", "TEXTO", "SENSIVEL", Texto("a"))
            .Derivado("SEM_BASE", "Sem base", "TEXTO", "SENSIVEL", Texto("b"), hipoteseLegal: " ");
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosSensiveisConsultar);

        InscricaoAdministrativaDto dto = await ConsultarDto(cenario, diogo);

        dto.Derivados.Select(static i => i.FatoCodigo).Should().Equal("COM_BASE");
    }
}
