namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Montagem comum dos testes do circuito de aprovação da proteção de dados do catálogo.</summary>
internal static class CatalogoDeProtecaoDeTeste
{
    public const string Isabella = "isabella.figueiredo";
    public const string Caroline = "caroline.lima@unifesspa.edu.br";
    public const string Finalidade = "Comprovação documental do candidato.";

    public static readonly DateTimeOffset Agora = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>O acesso de quem tem exatamente as permissões dadas.</summary>
    public static AcessoAoCatalogoDeProtecao AcessoCom(params PermissaoConfiguracao[] permitidas) => new(
        permitidas.Contains(PermissaoConfiguracao.TiposDocumentoManter),
        permitidas.Contains(PermissaoConfiguracao.FatosCandidatoManter),
        permitidas.Contains(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar));

    public static IUserContext UsuarioIsabella()
    {
        IUserContext usuario = Substitute.For<IUserContext>();
        usuario.UserId.Returns(Isabella);
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
}
