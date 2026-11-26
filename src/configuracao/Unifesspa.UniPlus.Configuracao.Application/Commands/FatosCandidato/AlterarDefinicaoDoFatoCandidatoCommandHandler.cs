namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Valida a definição nova no agregado e a confere contra o que cita o fato (ADR-0136); só então
/// grava (<see cref="AlteracaoConferidaDoFato"/>).
/// </summary>
public static class AlterarDefinicaoDoFatoCandidatoCommandHandler
{
    public static Task<Result> Handle(
        AlterarDefinicaoDoFatoCandidatoCommand command,
        AlteracaoConferidaDoFato alteracoes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(alteracoes);

        DefinicaoDoFato definicao = Ler(command.Definicao);

        return alteracoes.AplicarAsync(command.Id, (fato, catalogo) => fato.AlterarDefinicao(definicao, catalogo), cancellationToken);
    }

    private static DefinicaoDoFato Ler(DefinicaoDoFatoInput entrada) =>
        new(
            CodigoInformado<TipoDoDado>.Ler(entrada.Dominio),
            CodigoInformado<QuantidadeDeValores>.Ler(entrada.Cardinalidade),
            CodigoInformado<FonteValoresFato>.Ler(entrada.FonteValores),
            CodigoInformado<FormatoTexto>.Ler(entrada.Formato),
            entrada.PontoResolucao,
            CodigoInformado<EscopoFato>.Ler(entrada.Escopo),
            CodigoInformado<ClassificacaoDeProtecao>.Ler(entrada.ClassificacaoProtecao),
            entrada.FinalidadeTratamento,
            CodigoInformado<HipoteseLegalTratamento>.Ler(entrada.HipoteseLegal));
}
