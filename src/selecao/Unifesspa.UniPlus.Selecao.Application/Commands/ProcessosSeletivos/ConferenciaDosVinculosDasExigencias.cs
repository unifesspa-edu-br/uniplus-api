namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ProcessosSeletivos.Edicao;
using Domain.ProcessosSeletivos.Exigencias;
using Domain.ProcessosSeletivos.Referencias;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Confere no catálogo os dados e valores citados pelas exigências, uma recusa por campo do pedido que cita: a
/// condição do gatilho (o dado e o valor) e o dado que o documento comprova.
/// </summary>
internal static class ConferenciaDosVinculosDasExigencias
{
    /// <summary>
    /// As recusas de vínculo novo; sem os dados do catálogo, o pedido não cita dado nenhum. <paramref name="caminhosDasRegrasSemGrupo"/>
    /// traz o caminho de cada regra sem grupo no pedido.
    /// </summary>
    public static IReadOnlyList<FieldError> Conferir(
        ProcessoSeletivo processo, FatosDoGatilho? fatos, IReadOnlyList<RegraDeComprovacao> regrasSemGrupo, IReadOnlyList<string> caminhosDasRegrasSemGrupo)
    {
        if (fatos is null)
        {
            return [];
        }

        List<RegraDeComprovacao> regras = [.. regrasSemGrupo.SelectMany(static regra => regra.EnumerarRegraEItens())];
        IReadOnlyList<string> caminhos = CaminhosDasExigencias.DosDocumentos(regras, CaminhosDasExigencias.DasRegras(regrasSemGrupo, caminhosDasRegrasSemGrupo));
        IEnumerable<DocumentoExigido> documentos = regras.Select(static regra => regra.DocumentoExigido).OfType<DocumentoExigido>();
        return ConferenciaDeVinculoNovo.ConferirNosCampos(
            fatos.Catalogo,
            VinculosExistentes(processo),
            [.. documentos.SelectMany((documento, posicao) => CamposDoDocumento(documento, caminhos[posicao]))]);
    }

    private static IEnumerable<VinculosDoCampo> CamposDoDocumento(DocumentoExigido documento, string caminho)
    {
        string condicoes = $"{caminho}.{AplicabilidadeDaExigenciaInput.CaminhoDasCondicoes}";
        IEnumerable<VinculosDoCampo> condicionais = documento.Condicoes.SelectMany((condicao, posicao) => new[]
        {
            new VinculosDoCampo($"{condicoes}[{posicao}].{CondicaoGatilhoInput.CaminhoDoFato}", VinculosDeFatos.De([condicao.Fato], [])),
            new VinculosDoCampo($"{condicoes}[{posicao}].{CondicaoGatilhoInput.CaminhoDoValor}", SoOValor(condicao)),
        });
        IEnumerable<VinculosDoCampo> comprovados = documento.FatosComprovados.Select((fato, posicao) =>
            new VinculosDoCampo($"{caminho}.{ItemDocumentoExigidoInput.CaminhoDosFatosComprovados}[{posicao}]", VinculosDeFatos.De([fato], [])));
        return [.. condicionais, .. comprovados];
    }

    // O dado da condição é conferido no campo dele; no campo do valor só o valor comparado.
    private static VinculosDeFatos SoOValor(CondicaoGatilho condicao) =>
        VinculosDeFatos.De([], [(condicao.Fato, condicao.Valor)]) with { Fatos = new HashSet<string>(StringComparer.Ordinal) };

    private static VinculosDeFatos VinculosExistentes(ProcessoSeletivo processo)
    {
        VinculosDeFatos vivos = ReferenciasVivas.Vinculos(processo.Declaracao());
        IEnumerable<string> comprovados = processo.DocumentosExigidos.SelectMany(static d => d.FatosComprovados);
        return vivos with { Fatos = new HashSet<string>(vivos.Fatos.Concat(comprovados), StringComparer.Ordinal) };
    }
}
