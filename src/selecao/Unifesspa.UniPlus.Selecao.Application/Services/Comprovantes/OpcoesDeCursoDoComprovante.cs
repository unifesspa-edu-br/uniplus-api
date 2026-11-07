namespace Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os cursos que o envio escolheu, pelos ids gravados no comprovante, nunca pelos da inscrição, que uma correção posterior muda,
/// nomeados pela versão da inscrição, com as modalidades de cada opção.
/// </summary>
internal static class OpcoesDeCursoDoComprovante
{
    public static IReadOnlyList<OpcaoDeCursoDoComprovanteDto> De(InsumosDoComprovante insumos)
    {
        ComprovanteInscricao comprovante = insumos.Comprovante;
        IReadOnlyList<OfertaEscolhidaDto> ofertas = OfertasEscolhidas.Montar(
            comprovante.OfertaCursoId,
            comprovante.OfertaCursoSegundaOpcaoId,
            OfertasEscolhidas.NomesDe(ContextoDaVersaoFixadaResolver.OfertasDaVersao(insumos.Envelope.Grafo)));

        return [.. ofertas.Select(o => new OpcaoDeCursoDoComprovanteDto(o.Ordem, o.Nome, ModalidadesDa(comprovante, o.Ordem)))];
    }

    /// <summary>Quantas opções de curso a versão admite; uma só quando a versão não declara.</summary>
    public static int NumeroDeOpcoes(InsumosDoComprovante insumos) => insumos.Envelope.Grafo.NumeroDeOpcoesDeCurso ?? 1;

    private static List<ModalidadeDaInscricaoDto>? ModalidadesDa(ComprovanteInscricao comprovante, int ordem)
    {
        IReadOnlyList<ModalidadeDeConcorrencia>? modalidades = ordem == 1 ? comprovante.Modalidades : comprovante.ModalidadesSegundaOpcao;

        return modalidades is null ? null : [.. modalidades.Select(static m => new ModalidadeDaInscricaoDto(m.Codigo, m.Nome, m.Ordem))];
    }
}
