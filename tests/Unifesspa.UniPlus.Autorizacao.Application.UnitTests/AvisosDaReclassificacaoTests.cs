namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os avisos da reclassificação: a subida avisa a proteção de dados, o escopo e a divergência avisam também quem mantém
/// perfis, a descida não avisa, e cada fato grava uma só linha de aviso, sem dado pessoal no conteúdo.
/// </summary>
public sealed class AvisosDaReclassificacaoTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string EmailProtecao = "marlenemarciamartins@uniplus-test.local";
    private const string EmailManutencao = "diogo.bruno.souza@uniplus-test.local";
    private const string Codigo = "selecao:processos:consultar";

    private static readonly string CodigoProtecao = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados).Codigo;
    private static readonly string CodigoManutencao = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PerfisManter).Codigo;
    private static readonly DateTimeOffset Agora = PerfisDeTeste.Instante;

    [Fact(DisplayName = "A subida de classe avisa só quem aprova pela proteção de dados")]
    public async Task Subida_AvisaAProtecaoDeDados()
    {
        Cenario cenario = new();
        ReclassificacaoDaPermissao subida = cenario.Reclassificar(new MudancaDaPermissao(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Sensivel, false, false));

        IReadOnlyList<EnviarEmail>? emails = await cenario.Avisos.MontarAsync(new FatoSemAviso(OrigemDoAviso.Reclassificacao, subida.Id), CancellationToken.None);

        emails!.Select(e => e.Destinatario).Should().BeEquivalentTo([EmailProtecao]);
    }

    [Fact(DisplayName = "A mudança de escopo e a subida juntas avisam a proteção de dados e quem mantém perfis")]
    public async Task EscopoESubida_AvisamOsDoisGrupos()
    {
        Cenario cenario = new();
        ReclassificacaoDaPermissao fato = cenario.Reclassificar(new MudancaDaPermissao(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Sensivel, false, true));

        IReadOnlyList<EnviarEmail>? emails = await cenario.Avisos.MontarAsync(new FatoSemAviso(OrigemDoAviso.Reclassificacao, fato.Id), CancellationToken.None);

        emails!.Select(e => e.Destinatario).Should().BeEquivalentTo([EmailProtecao, EmailManutencao]);
        emails![0].CorpoTexto.Should().Contain("classificação de interno para sensivel").And.Contain("escopo de institucional para de unidade");
    }

    [Fact(DisplayName = "A divergência da governança avisa os dois grupos")]
    public async Task Divergencia_AvisaOsDoisGrupos()
    {
        Cenario cenario = new();
        DivergenciaDaGovernanca divergencia = DivergenciaDaGovernanca.DaComposicao(
            Guid.CreateVersion7(), cenario.PerfilId, Codigo, TipoDeDivergencia.ComposicaoFaltante, Agora, 0);
        cenario.Repositorio.Divergencias.Add(divergencia);

        IReadOnlyList<EnviarEmail>? emails = await cenario.Avisos.MontarAsync(new FatoSemAviso(OrigemDoAviso.Divergencia, divergencia.Id), CancellationToken.None);

        emails!.Select(e => e.Destinatario).Should().BeEquivalentTo([EmailProtecao, EmailManutencao]);
    }

    [Fact(DisplayName = "A descida de classe não avisa")]
    public async Task Descida_NaoAvisa()
    {
        Cenario cenario = new();
        ReclassificacaoDaPermissao descida = cenario.Reclassificar(new MudancaDaPermissao(ClassificacaoDeProtecao.Sensivel, ClassificacaoDeProtecao.Interno, false, false));

        IReadOnlyList<EnviarEmail>? emails = await cenario.Avisos.MontarAsync(new FatoSemAviso(OrigemDoAviso.Reclassificacao, descida.Id), CancellationToken.None);

        emails.Should().BeNull();
    }

    [Fact(DisplayName = "O conteúdo traz o perfil, a permissão, a mudança e o link, e nenhum e-mail nem identidade de pessoa")]
    public async Task Conteudo_SemDadoPessoal()
    {
        Cenario cenario = new();
        ReclassificacaoDaPermissao fato = cenario.Reclassificar(new MudancaDaPermissao(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Sensivel, false, false));

        IReadOnlyList<EnviarEmail>? emails = await cenario.Avisos.MontarAsync(new FatoSemAviso(OrigemDoAviso.Reclassificacao, fato.Id), CancellationToken.None);

        string texto = emails![0].CorpoTexto;
        texto.Should().Contain("Perfil de teste").And.Contain(Codigo).And.Contain($"https://uniplus.test/configuracao/perfis/{cenario.PerfilId}");
        texto.Should().NotContain(EmailProtecao).And.NotContain(EmailManutencao).And.NotContain("marlene");
    }

    [Fact(DisplayName = "O comando grava uma linha de aviso por fato e devolve os e-mails para o outbox")]
    public async Task Comando_GravaALinhaEDevolveOsEmails()
    {
        Cenario cenario = new();
        ReclassificacaoDaPermissao fato = cenario.Reclassificar(new MudancaDaPermissao(ClassificacaoDeProtecao.Interno, ClassificacaoDeProtecao.Sensivel, false, false));
        DoublesDePerfis.UnidadeDeTrabalho unidade = new();

        (Result<bool> resposta, IEnumerable<object> mensagens) = await EnviarAvisoDaReclassificacaoCommandHandler.Handle(
            new EnviarAvisoDaReclassificacaoCommand(OrigemDoAviso.Reclassificacao, fato.Id),
            cenario.Avisos, cenario.Repositorio, unidade, TimeProvider.System, CancellationToken.None);

        resposta.Value.Should().BeTrue();
        mensagens.Should().ContainSingle();
        cenario.Repositorio.Avisos.Should().ContainSingle(a => a.Origem == OrigemDoAviso.Reclassificacao && a.OrigemId == fato.Id);
        unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "O fato que não existe mais não grava aviso nem e-mail")]
    public async Task Comando_FatoInexistente_NaoGrava()
    {
        Cenario cenario = new();
        DoublesDePerfis.UnidadeDeTrabalho unidade = new();

        (Result<bool> resposta, IEnumerable<object> mensagens) = await EnviarAvisoDaReclassificacaoCommandHandler.Handle(
            new EnviarAvisoDaReclassificacaoCommand(OrigemDoAviso.Divergencia, Guid.NewGuid()),
            cenario.Avisos, cenario.Repositorio, unidade, TimeProvider.System, CancellationToken.None);

        resposta.Value.Should().BeFalse();
        mensagens.Should().BeEmpty();
        cenario.Repositorio.Avisos.Should().BeEmpty();
        unidade.Gravacoes.Should().Be(0);
    }

    private sealed class Cenario
    {
        public Cenario()
        {
            Repositorio.NomesDosPerfis[PerfilId] = "Perfil de teste";
            Avisos = new AvisosDaReclassificacao(
                new EmissorDeAvisos(new CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria(), DoublesDeAvisos.MetricasDeTeste, NullLogger<EmissorDeAvisos>.Instance),
                new GruposDaGovernanca(new TitularesPorGrupo()),
                new DoublesDeAvisos.LinkDeTeste(),
                new CatalogoDaAplicacao([]),
                Repositorio);
        }

        public Guid PerfilId { get; } = Guid.CreateVersion7();

        public ReclassificacoesNaMemoria Repositorio { get; } = new();

        public AvisosDaReclassificacao Avisos { get; }

        public ReclassificacaoDaPermissao Reclassificar(MudancaDaPermissao mudanca)
        {
            ReclassificacaoDaPermissao fato = ReclassificacaoDaPermissao.Registrar(Guid.CreateVersion7(), PerfilId, Codigo, mudanca, Agora, 0);
            Repositorio.Reclassificacoes.Add(fato);

            return fato;
        }
    }

    private sealed class TitularesPorGrupo : ITitularesDaPermissao
    {
        public Task<IReadOnlyList<string>> ListarEmailsAsync(string codigoDaPermissao, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TitularDaPermissao>> ListarTitularesComEmailAsync(string codigoDaPermissao, CancellationToken cancellationToken = default)
        {
            TitularDaPermissao[] titulares = codigoDaPermissao == CodigoProtecao
                ? [new(Emissor, "marlene.martins", EmailProtecao)]
                : codigoDaPermissao == CodigoManutencao ? [new(Emissor, "diogo.souza", EmailManutencao)] : [];

            return Task.FromResult<IReadOnlyList<TitularDaPermissao>>(titulares);
        }
    }

}
