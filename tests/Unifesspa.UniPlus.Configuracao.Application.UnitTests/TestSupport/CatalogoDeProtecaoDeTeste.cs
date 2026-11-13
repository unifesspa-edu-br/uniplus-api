namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Montagem comum dos testes do circuito de aprovação da proteção de dados do catálogo de tipos de documento e de fatos
/// do candidato.
/// </summary>
internal static class CatalogoDeProtecaoDeTeste
{
    public const string Isabella = "isabella.figueiredo";
    public const string Caroline = "caroline.lima@unifesspa.edu.br";
    public const string Emissor = "https://sso.unifesspa.edu.br/realms/unifesspa";
    public const string Finalidade = "Comprovação documental do candidato.";

    public static readonly DateTimeOffset Agora = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>O verificador de quem tem exatamente as permissões dadas: avalia cada uma como permitida ou negada.</summary>
    public static IVerificadorDeAcesso VerificadorCom(params PermissaoConfiguracao[] permitidas)
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(chamada =>
        {
            PermissionRequirement requisito = chamada.ArgAt<PermissionRequirement>(0);
            return Task.FromResult(DecisaoDeAcessoDeTeste.Tomada(
                permitidas.Any(p => p.Codigo == requisito.Permissao) ? ResultadoDoAcesso.Permitido : ResultadoDoAcesso.Negado,
                requisito,
                chamada.ArgAt<ResourceContext?>(1)));
        });

        return verificador;
    }

    public static IUserContext UsuarioIsabella()
    {
        IUserContext usuario = Substitute.For<IUserContext>();
        usuario.UserId.Returns(Isabella);
        usuario.Issuer.Returns(Emissor);
        usuario.NomeCompleto.Returns("Isabella Figueiredo");
        return usuario;
    }

    public static TimeProvider Relogio()
    {
        TimeProvider relogio = Substitute.For<TimeProvider>();
        relogio.GetUtcNow().Returns(Agora);
        return relogio;
    }

    public static TipoDocumento NovoTipo(
        string codigo = "LAUDO_MEDICO_COMPLEMENTAR",
        string nome = "Laudo médico complementar",
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Interno) =>
        TipoDocumento.Criar(
            codigo, nome, null, "SAUDE", null, null, null, classificacao, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;

    public static FatoCandidato NovoFato(string codigo = "TEM_RENDA", string nome = "Tem renda") =>
        FatoCandidato.CriarDoAdministrador(
            codigo, nome, "Descrição do fato.", DominioFato.Booleano, CardinalidadeFato.Escalar, null, null, "INSCRICAO", EscopoFato.Candidato,
            ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;

    /// <summary>O tipo no instante do ato, como o registro do ato o guarda.</summary>
    public static RetratoDoItemDaProtecao Retrato(TipoDocumento tipo) =>
        new(new ItemDaProtecao(CadastroDoCatalogo.TipoDocumento, tipo.Id), tipo.Codigo.Valor, tipo.Nome, tipo.Protecao,
            tipo.Aprovacao.Ciclo, tipo.Aprovacao.ManifestacaoDesfavoravelVigente);

    /// <summary>O fato no instante do ato, como o registro do ato o guarda.</summary>
    public static RetratoDoItemDaProtecao Retrato(FatoCandidato fato) =>
        new(new ItemDaProtecao(CadastroDoCatalogo.Fato, fato.Id), fato.Codigo, fato.Nome, fato.Protecao,
            fato.Aprovacao.Ciclo, fato.Aprovacao.ManifestacaoDesfavoravelVigente);
}
