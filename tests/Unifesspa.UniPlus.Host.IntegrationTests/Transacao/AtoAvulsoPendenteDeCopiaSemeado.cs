namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um processo com a divulgação do certame na versão 1 e um ato publicado que não criou versão: o ato já entrou na
/// divulgação sem endereço, e a cópia dos documentos dele ao acervo ainda não aconteceu.
/// </summary>
internal sealed record AtoAvulsoPendenteDeCopiaSemeado(Guid ProcessoId, Guid AtoId)
{
    internal static async Task<AtoAvulsoPendenteDeCopiaSemeado> SemearAsync(IServiceProvider servicos)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = NovoProcesso();
        DocumentoAPublicar documento = NovoDocumentoConfirmado(processo.Id);
        Guid atoId = Guid.CreateVersion7();

        contexto.Set<ProcessoSeletivo>().Add(processo);
        contexto.Set<DocumentoAPublicar>().Add(documento);
        contexto.Set<AtoDoProcesso>().Add(NovoAto(processo, atoId, documento.Id));
        contexto.Set<CertameDivulgado>().Add(NovaDivulgacaoComEntradaPendente(processo, atoId));
        await contexto.SaveChangesAsync();

        return new AtoAvulsoPendenteDeCopiaSemeado(processo.Id, atoId);
    }

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar(
            "Processo da cópia ao acervo", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            IdentificadorLegivel.Criar($"certame-{Guid.NewGuid():N}"[..40]).Value!);

    private static DocumentoAPublicar NovoDocumentoConfirmado(Guid processoId)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.DocumentoDeAto, "aviso.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, new string('c', 64), TimeProvider.System);

        return documento;
    }

    private static AtoDoProcesso NovoAto(ProcessoSeletivo processo, Guid atoId, Guid documentoId) =>
        AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                atoId, processo.Id, "AVISO", "AVISO", Numero: null, new DateOnly(2026, 10, 1), "Diretoria do CEPS", "ceps",
                processo.IdentificadorLegivel!.Value.Valor, documentoId, []),
            DateTimeOffset.UtcNow).Value!;

    private static CertameDivulgado NovaDivulgacaoComEntradaPendente(ProcessoSeletivo processo, Guid atoId)
    {
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        CertameDivulgado divulgado = CertameDivulgado.Criar(
            processo.Id, numeroVersao: 1, Guid.CreateVersion7(), new string('a', 64), "teste",
            new FacetasDoCertameDivulgado(processo.IdentificadorLegivel!.Value.Valor, processo.Nome, "001/2026", ["AC"], agora.AddDays(-1), agora.AddDays(20)),
            "{}", agora);
        divulgado.AbrirEntradaDoAto(atoId, numeroVersao: null);

        return divulgado;
    }
}
