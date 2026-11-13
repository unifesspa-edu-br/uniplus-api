namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>O processo de seleção sem nenhuma dimensão configurada, a que cada teste acrescenta o que avalia.</summary>
internal static class ProcessosDeTeste
{
    public static ProcessoSeletivo SemConfiguracao(string nome, OrigemCandidatos origem) =>
        ProcessoSeletivo.Criar(
            nome, TipoProcesso.SiSU, origem, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());
}
