namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Monta o registro do ato do processo para o ato que cria uma versão de configuração — a abertura e
/// a retificação do edital.
/// </summary>
/// <remarks>
/// O registro nasce junto da versão e do ato em Publicações, na mesma transação. Tem o
/// mesmo formato do registro do ato que não cria versão, e é o que dá à consulta dos atos do processo
/// uma fonte única. O ato é publicado no instante em que a versão passa a vigorar, o mesmo do ato entregue ao registro central.
/// A unidade e o identificador legível são os do processo no instante da publicação;
/// o documento principal é o do edital e os anexos são os da versão que nasce, na ordem em que o edital os
/// declara — os herdados, quando a retificação não traz lista nova.
/// </remarks>
internal static class RegistroDoAtoDaVersao
{
    public static Result<AtoDoProcesso> Montar(
        ProcessoSeletivo processo, VersaoConfiguracao versao, AtoEDocumentoConferidos ato, DadosEdital edital, RetificacaoDoEdital? retificacao)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(edital);

        return AtoDoProcesso.Registrar(NovoAtoDaVersao(processo, versao, ato, edital, retificacao), versao.VigenteAPartirDe);
    }

    private static NovoAtoDoProcesso NovoAtoDaVersao(
        ProcessoSeletivo processo, VersaoConfiguracao versao, AtoEDocumentoConferidos ato, DadosEdital edital, RetificacaoDoEdital? retificacao) =>
        new(
            AtoId: versao.AtoCriadorId,
            ProcessoSeletivoId: processo.Id,
            TipoCodigo: ato.Ato.TipoAtoCodigo,
            TipoNome: ato.TipoDoAto.Nome,
            Numero: edital.Numero,
            DataDoAto: ato.Ato.DataDoAto,
            Assinante: ato.Ato.Assinante,
            IdentificadorNoEnderecoWebDaUnidadeAdministradora: processo.UnidadeAdministradora.IdentificadorNoEnderecoWeb,
            IdentificadorLegivelDoProcesso: processo.IdentificadorLegivel!.Value.Valor,
            DocumentoPrincipalId: ato.Documento.Id,
            Anexos: [.. edital.Anexos.Select(static a => new AnexoDeclarado(a.DocumentoId, a.NomeArquivo))],
            AtoCorrigidoId: retificacao?.EditalRetificadoId,
            MotivoDaCorrecao: retificacao?.Motivo);
}
