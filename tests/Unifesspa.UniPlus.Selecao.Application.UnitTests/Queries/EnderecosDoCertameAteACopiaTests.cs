namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CertameDivulgadoDeTeste;

/// <summary>
/// O endereço dos documentos do certame só chega ao público quando a cópia ao acervo termina.
/// </summary>
public sealed class EnderecosDoCertameAteACopiaTests
{
    private const string EnderecoDoEdital = "https://acervo.teste/abertura.pdf";

    [Fact(DisplayName = "Antes da cópia o edital da versão é servido sem endereço, e o ato criador aparece como entrada pendente")]
    public async Task Detalhe_AntesDaCopia_EditalSemEndereco()
    {
        (Guid processoId, CertameDivulgado divulgado, CertamePublicadoDto projecao) = Cenario();
        divulgado.RegistrarEditalPendente(projecao.AtoCriadorId, 1);

        CertamePublicadoDto servido = await LerAsync(processoId, divulgado);

        servido.DocumentoEdital.UrlDownload.Should().BeNull();
        servido.EditaisPublicados.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new EditalPublicadoCertameDto(projecao.AtoCriadorId, null, []));
    }

    [Fact(DisplayName = "Depois da cópia o edital da versão e o da entrada do ato trazem o endereço")]
    public async Task Detalhe_DepoisDaCopia_EditalComEndereco()
    {
        (Guid processoId, CertameDivulgado divulgado, CertamePublicadoDto projecao) = Cenario();
        divulgado.RegistrarEditalPendente(projecao.AtoCriadorId, 1);
        divulgado.RegistrarEnderecos(projecao.AtoCriadorId, EnderecoDoEdital, []);

        CertamePublicadoDto servido = await LerAsync(processoId, divulgado);

        servido.DocumentoEdital.UrlDownload.Should().Be(projecao.DocumentoEdital.UrlDownload);
        servido.EditaisPublicados.Should().ContainSingle().Which.UrlDownload!.OriginalString.Should().Be(EnderecoDoEdital);
    }

    [Fact(DisplayName = "A cópia de um ato que não criou a versão vigente não libera o endereço do edital da versão")]
    public async Task Detalhe_CopiaDeOutroAto_MantemOEditalDaVersaoSemEndereco()
    {
        (Guid processoId, CertameDivulgado divulgado, CertamePublicadoDto projecao) = Cenario();
        Guid aviso = Guid.CreateVersion7();
        divulgado.RegistrarEditalPendente(projecao.AtoCriadorId, 1);
        divulgado.RegistrarEditalPendente(aviso, null);
        divulgado.RegistrarEnderecos(aviso, "https://acervo.teste/aviso.pdf", []);

        CertamePublicadoDto servido = await LerAsync(processoId, divulgado);

        servido.DocumentoEdital.UrlDownload.Should().BeNull();
        servido.EditaisPublicados!.Single(e => e.AtoId == aviso).UrlDownload.Should().NotBeNull();
    }

    [Fact(DisplayName = "O selo do detalhe muda quando o endereço do edital chega")]
    public async Task Selo_MudaQuandoOEnderecoChega()
    {
        (Guid processoId, CertameDivulgado divulgado, CertamePublicadoDto projecao) = Cenario();
        divulgado.RegistrarEditalPendente(projecao.AtoCriadorId, 1);
        string antes = SeloDoCertamePublicado.De(await LerAsync(processoId, divulgado));

        divulgado.RegistrarEnderecos(projecao.AtoCriadorId, EnderecoDoEdital, []);
        string depois = SeloDoCertamePublicado.De(await LerAsync(processoId, divulgado));

        depois.Should().NotBe(antes);
    }

    private static (Guid ProcessoId, CertameDivulgado Divulgado, CertamePublicadoDto Projecao) Cenario()
    {
        Guid processoId = Guid.CreateVersion7();
        CertamePublicadoDto projecao = Projecao(processoId);
        return (processoId, Divulgado(processoId, projecao), projecao);
    }

    private static async Task<CertamePublicadoDto> LerAsync(Guid processoId, CertameDivulgado divulgado)
    {
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        repository.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>()).Returns(divulgado);

        Result<CertamePublicadoDto> resultado = await ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(processoId, Agora), repository, CancellationToken.None);

        return resultado.Value!;
    }
}
