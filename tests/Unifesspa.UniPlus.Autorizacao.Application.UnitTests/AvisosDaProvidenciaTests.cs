namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Quem recebe cada aviso da providência e o que ele não pode trazer (UNI-REQ-0375). Cecília solicita, Leandro é o
/// beneficiário, Diogo atende; Diogo, Marlene, Cecília e Leandro administram concessões.
/// </summary>
public sealed class AvisosDaProvidenciaTests
{
    private readonly CenarioDaProvidencia _cenario = new();

    [Fact(DisplayName = "A abertura avisa todos os que administram concessões, inclusive quem solicitou, menos o beneficiário")]
    public async Task Abertura_AvisaAdministracaoMenosOBeneficiario()
    {
        IReadOnlyList<EnviarEmail> avisos = await AvisarAsync(new ProvidenciaBuilder(), static b => b.Aberta());

        avisos.Select(static a => a.Destinatario).Should().BeEquivalentTo(
            [CenarioDaProvidencia.EmailDiogo, CenarioDaProvidencia.EmailMarlene, ProvidenciaBuilder.Cecilia.Email!]);
        avisos.Should().OnlyContain(static a => a.Finalidade == "perfil.providencia_solicitada");
    }

    [Fact(DisplayName = "O aviso da abertura não traz o fundamento nem nome de pessoa, só o perfil e o link da providência")]
    public async Task Abertura_SemFundamentoNemNome()
    {
        ProvidenciaBuilder cenario = new();
        IReadOnlyList<EnviarEmail> avisos = await AvisarAsync(cenario, static b => b.Aberta());

        EnviarEmail aviso = avisos[0];
        string[] conteudo = [aviso.Assunto, aviso.CorpoTexto, aviso.CorpoHtml];
        conteudo.Should().AllSatisfy(static t => t.Should().NotContain(ProvidenciaBuilder.Fundamento)
            .And.NotContain(ProvidenciaBuilder.Cecilia.Nome!).And.NotContain(ProvidenciaBuilder.Leandro.Nome!));
        aviso.CorpoTexto.Should().Contain(CenarioDaProvidencia.PerfilNome).And.Contain("/acessos/providencias/");
    }

    [Fact(DisplayName = "A resposta avisa só quem solicitou, pelo e-mail gravado na abertura, sem o motivo")]
    public async Task Resposta_AvisaQuemSolicitou()
    {
        IReadOnlyList<EnviarEmail> avisos = await AvisarAsync(new ProvidenciaBuilder(), static b => b.Respondida());

        EnviarEmail aviso = avisos.Should().ContainSingle().Subject;
        (aviso.Destinatario, aviso.Finalidade).Should().Be((ProvidenciaBuilder.Cecilia.Email!, "perfil.providencia_respondida"));
        aviso.CorpoTexto.Should().NotContain(ProvidenciaBuilder.Motivo).And.NotContain(ProvidenciaBuilder.Diogo.Nome!);
    }

    [Fact(DisplayName = "O atendimento avisa quem solicitou e o beneficiário, cada um com o seu aviso")]
    public async Task Atendimento_AvisaQuemSolicitouEOBeneficiario()
    {
        IReadOnlyList<EnviarEmail> avisos = await AvisarAsync(new ProvidenciaBuilder(), static b => b.Atendida());

        avisos.Select(static a => (a.Destinatario, a.Finalidade)).Should().BeEquivalentTo(
        [
            (ProvidenciaBuilder.Cecilia.Email!, "perfil.providencia_atendida"),
            (ProvidenciaBuilder.Leandro.Email!, "perfil.concessao_revogada_no_atendimento"),
        ]);
    }

    [Fact(DisplayName = "O aviso ao beneficiário não identifica quem solicitou nem quem atendeu, não fala em providência e orienta a pedir de novo")]
    public async Task Atendimento_AvisoAoBeneficiario_NaoIdentificaNinguem()
    {
        EnviarEmail aviso = (await AvisarAsync(new ProvidenciaBuilder(), static b => b.Atendida()))
            .Single(static a => a.Destinatario == ProvidenciaBuilder.Leandro.Email);

        string[] proibidos =
        [
            ProvidenciaBuilder.Cecilia.Nome!, ProvidenciaBuilder.Cecilia.Identidade.Sub, ProvidenciaBuilder.Cecilia.Email!,
            ProvidenciaBuilder.Diogo.Nome!, ProvidenciaBuilder.Diogo.Sub, CenarioDaProvidencia.EmailDiogo,
            ProvidenciaBuilder.Fundamento, ProvidenciaBuilder.Motivo, "providência", "Providência",
        ];
        new[] { aviso.Assunto, aviso.CorpoTexto, aviso.CorpoHtml }.Should().AllSatisfy(t => proibidos.Should().AllSatisfy(p => t.Should().NotContain(p)));
        aviso.CorpoTexto.Should().Contain(CenarioDaProvidencia.PerfilNome).And.Contain("nova solicitação comprovando a necessidade").And.Contain("/pedidos/novo");
    }

    [Fact(DisplayName = "Sem e-mail verificado de quem solicitou, a abertura registra no log, sem dado pessoal, e a resposta não tem para quem avisar")]
    public async Task SemEmailDeQuemSolicitou_NaoAvisaERegistra()
    {
        PessoaNoToken naoVerificada = ProvidenciaBuilder.Cecilia with { EmailVerificado = false };
        ProvidenciaBuilder cenario = new ProvidenciaBuilder().SolicitadaPor(naoVerificada);

        Providencia aberta = cenario.Aberta();
        await AvisarAsync(cenario, _ => aberta);

        _cenario.Logger.Mensagens.Should().ContainSingle(m => m.Contains(aberta.Id.ToString(), StringComparison.Ordinal))
            .Which.Should().NotContain(naoVerificada.Identidade.Sub).And.NotContain(naoVerificada.Nome!);
        (await AvisarAsync(new ProvidenciaBuilder().SolicitadaPor(naoVerificada), static b => b.Respondida())).Should().BeEmpty();
    }

    [Fact(DisplayName = "O mesmo aviso não sai duas vezes para o mesmo ato")]
    public async Task MesmoAto_NaoRepete()
    {
        ProvidenciaBuilder cenario = new();
        Providencia respondida = cenario.Respondida();

        (await AvisarAsync(cenario, _ => respondida)).Should().ContainSingle();
        (await AvisarAsync(cenario, _ => respondida)).Should().BeEmpty();
    }

    private Task<IReadOnlyList<EnviarEmail>> AvisarAsync(ProvidenciaBuilder cenario, Func<ProvidenciaBuilder, Providencia> montar) =>
        _cenario.Avisos.AvisarAsync(
            new ProvidenciaComConcessao(montar(cenario), new ConcessaoDaProvidencia(cenario.Concessao, CenarioDaProvidencia.PerfilNome)), CancellationToken.None);
}
