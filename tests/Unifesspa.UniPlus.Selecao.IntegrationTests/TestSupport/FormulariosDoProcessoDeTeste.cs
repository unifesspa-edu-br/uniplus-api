namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os formulários do processo lidos do banco pelo código, que é o que os identifica, e os itens que
/// pertencem a cada um pelo vínculo com o formulário.
/// </summary>
internal static class FormulariosDoProcessoDeTeste
{
    public const string Inscricao = "INSCRICAO";

    public const string PedidoDeIsencao = "PEDIDO_DE_ISENCAO";

    public const string Habilitacao = "HABILITACAO";

    public static CodigoDoFormulario Codigo(string valor) => CodigoDoFormulario.Criar(valor).Value!;

    /// <summary>O formulário do código dado, na fase dada, com a obrigatoriedade dada.</summary>
    public static FormularioDeclarado Declarado(string codigo, Guid? faseId, bool obrigatorio) => new(Codigo(codigo), faseId, obrigatorio);

    /// <summary>
    /// As páginas de um formulário que não serve à inscrição: uma seção de dados e a revisão e aceite, sem a
    /// seção reservada do conjunto básico.
    /// </summary>
    public static IReadOnlyList<PaginaDoFormulario> PaginasSemConjuntoBasico() =>
    [
        PaginaDoFormulario.Criar(new PaginaEstrutura(FormularioDeTeste.Secao, 0, TipoPaginaFormulario.Secao, null), "Dados", null, null).Value!,
        PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
    ];

    /// <summary>Define o formulário que não serve à inscrição e falha o teste se o processo o recusar.</summary>
    public static void DefinirFormularioSemConjuntoBasico(this ProcessoSeletivo processo, string codigo, Guid faseId, bool obrigatorio)
    {
        ArgumentNullException.ThrowIfNull(processo);

        Result definido = processo.DefinirFormulario(Declarado(codigo, faseId, obrigatorio), null, PaginasSemConjuntoBasico());
        if (definido.IsFailure)
        {
            throw new InvalidOperationException($"O formulário {codigo} de teste não pôde ser definido: {definido.Error?.Message}");
        }
    }

    /// <summary>O identificador do formulário do código dado no processo, lido do banco.</summary>
    public static async Task<Guid> IdentificadorDoFormularioAsync(UniPlusDbContext db, Guid processoId, string codigo)
    {
        ArgumentNullException.ThrowIfNull(db);

        List<FormularioProcesso> formularios = await db.Set<FormularioProcesso>().AsNoTracking()
            .Where(f => f.ProcessoSeletivoId == processoId).ToListAsync();
        return formularios.Single(f => string.Equals(f.Codigo.Valor, codigo, StringComparison.Ordinal)).Id;
    }

    public static FormularioProcesso? FormularioDoCodigo(this ProcessoSeletivo processo, string codigo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        return processo.Formularios.SingleOrDefault(f => string.Equals(f.Codigo.Valor, codigo, StringComparison.Ordinal));
    }

    public static IEnumerable<FatoColetado> CamposDoFormulario(this ProcessoSeletivo processo, string codigo)
    {
        Guid? formularioId = processo.FormularioDoCodigo(codigo)?.Id;
        return processo.Campos.Where(c => formularioId is not null && c.FormularioId == formularioId);
    }

    public static IEnumerable<TermoExigidoFormulario> TermosDoFormulario(this ProcessoSeletivo processo, string codigo)
    {
        Guid? formularioId = processo.FormularioDoCodigo(codigo)?.Id;
        return processo.TermosExigidos.Where(t => formularioId is not null && t.FormularioId == formularioId);
    }
}
