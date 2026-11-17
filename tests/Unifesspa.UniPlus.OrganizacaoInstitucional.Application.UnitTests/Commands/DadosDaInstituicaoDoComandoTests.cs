namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Os comandos mantêm o corpo plano da requisição; estas extensões entregam ao agregado cada campo
/// no lugar certo. Valores distintos por campo, para uma troca de posição não passar despercebida.
/// </summary>
public sealed class DadosDaInstituicaoDoComandoTests
{
    private static readonly IdentificacaoRegulatoriaDaInstituicao IdentificacaoEsperada = new(
        "codigo-emec", "nome", "sigla", "organizacao", "categoria", "cnpj", "mantenedora",
        "codigo-mantenedora", "situacao", "ato-credenciamento", "ato-recredenciamento", "conceito", "igc", "website");

    private static readonly CidadeDaSede CidadeEsperada = new("codigo-ibge", "cidade", "uf", null, null);

    private static CriarInstituicaoCommand ComandoDeCriacao() => new(
        "codigo-emec", "nome", "sigla", "organizacao", "categoria", "cnpj", "mantenedora",
        "codigo-mantenedora", "situacao", "ato-credenciamento", "ato-recredenciamento", "conceito", "igc", "website",
        null, "codigo-ibge", "cidade", "uf", null);

    private static AtualizarInstituicaoCommand ComandoDeAtualizacao() => new(
        Guid.CreateVersion7(),
        "codigo-emec", "nome", "sigla", "organizacao", "categoria", "cnpj", "mantenedora",
        "codigo-mantenedora", "situacao", "ato-credenciamento", "ato-recredenciamento", "conceito", "igc", "website",
        null, "codigo-ibge", "cidade", "uf", null);

    [Fact(DisplayName = "O comando de criação entrega os quatorze campos da identificação no lugar de cada um")]
    public void Identificacao_ComandoDeCriacao_MapeiaOsQuatorzeCampos()
    {
        ComandoDeCriacao().Identificacao().Should().Be(IdentificacaoEsperada);
    }

    [Fact(DisplayName = "O comando de atualização entrega os quatorze campos da identificação no lugar de cada um")]
    public void Identificacao_ComandoDeAtualizacao_MapeiaOsQuatorzeCampos()
    {
        ComandoDeAtualizacao().Identificacao().Should().Be(IdentificacaoEsperada);
    }

    [Fact(DisplayName = "A cidade do comando de criação traz o trio, sem proveniência nem carimbo")]
    public void Cidade_ComandoDeCriacao_TrazSoOTrio()
    {
        ComandoDeCriacao().Cidade().Should().Be(CidadeEsperada);
    }

    [Fact(DisplayName = "A cidade do comando de atualização traz o trio, sem proveniência nem carimbo")]
    public void Cidade_ComandoDeAtualizacao_TrazSoOTrio()
    {
        ComandoDeAtualizacao().Cidade().Should().Be(CidadeEsperada);
    }
}
