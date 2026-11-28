namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Monta o <see cref="AtoNormativo"/> de um <see cref="RegistrarAtoNormativoCommand"/>: confere a
/// forma do pedido pelo domínio, resolve o tipo e a cadeia de retificação no banco, junta todas as
/// recusas na ordem do corpo do pedido e, sem nenhuma recusa, reserva as vagas do objeto. Não grava o
/// ato: quem chama o adiciona e salva.
/// </summary>
public sealed class MontagemDoAtoNormativo(
    ITipoAtoPublicadoRepository tipos,
    IResolvedorFusoInstitucional fuso,
    CadeiaDeRetificacaoDoAto cadeia,
    ReservaDasVagasDoObjeto reservaDasVagas)
{
    private const string RegistroPeloDominioExigidoMensagem =
        "Os atos deste objeto são registrados pelo módulo dono dele, inclusive a retificação: este endpoint "
        + "não recebe o arquivo do documento, e o ato ficaria sem documento. Publique o ato pelo módulo dono do objeto.";

    public async Task<Result<AtoMontado>> MontarAsync(
        RegistrarAtoNormativoCommand command, DateTimeOffset publicadoEm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        DeclaracaoDoAtoNormativo declaracao = DeclaracaoDe(command);
        ConferenciaDaDeclaracaoDoAto forma = ConferenciaDaDeclaracaoDoAto.De(declaracao);
        TipoResolvido tipo = await ResolverTipoAsync(command, forma.CodigoDoTipo, publicadoEm, cancellationToken).ConfigureAwait(false);
        RetificacaoLida retificacao = await LerRetificacaoAsync(command, forma.Retificacao, tipo.Tipo, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FieldError> recusas = NaOrdemDoPedido(command, declaracao, forma, tipo, retificacao);

        return recusas.Count > 0
            ? Result<AtoMontado>.ValidationFailure(recusas)
            : await MontarSemRecusaAsync(command, publicadoEm, forma, tipo.Tipo!.Value, retificacao.Herdados, cancellationToken).ConfigureAwait(false);
    }

    private static DeclaracaoDoAtoNormativo DeclaracaoDe(RegistrarAtoNormativoCommand command) =>
        new()
        {
            Numeracao = new NumeracaoDoAto(command.Orgao ?? string.Empty, command.Serie ?? string.Empty, command.Ano, command.Numero),
            TipoCodigo = command.TipoCodigo,
            DataDoAto = command.DataDoAto,
            DocumentoHash = command.DocumentoHash,
            Assinante = command.Assinante,
            VersaoDoObjetoId = command.VersaoDoObjetoId,
            VersaoDoObjetoHash = command.VersaoDoObjetoHash,
            Retificacao = new RelacaoDeRetificacao(command.AtoRetificadoId, command.MotivoRetificacao),
            Vinculos = [.. (command.Vinculos ?? []).Select(VinculoDeclarado)],
            Anexos = [.. (command.Anexos ?? []).Select(AnexoDeclarado)],
        };

    private static EntidadeVinculada? VinculoDeclarado(VinculoEntidadeInput? vinculo) =>
        vinculo is null ? null : new EntidadeVinculada(vinculo.EntidadeTipo ?? string.Empty, vinculo.EntidadeId);

    private static (string? Nome, string? TipoMidia, string? Hash)? AnexoDeclarado(AnexoDoAtoInput? anexo) =>
        anexo is null ? null : (anexo.Nome, anexo.TipoMidia, anexo.Hash);

    /// <summary>
    /// Os atributos de consequência do tipo ou vêm CONFERIDOS pelo domínio que publicou (registro
    /// pela porta de escrita, ADR-0061), ou se resolvem agora no catálogo (registro pelo
    /// endpoint, em que quem publica está aqui, neste instante). Quando vêm conferidos o catálogo
    /// NÃO é relido: o cadastro é editável, e reler faria um edital publicado como congelante ser
    /// registrado com um tipo que já não congela. O nome do tipo segue o mesmo caminho, porque é
    /// ele que compõe o título público do ato. A versão vigente é a do dia civil da publicação no fuso
    /// institucional: a data que o documento declara não escolhe o tipo. O catálogo só é lido para o
    /// código aceito na forma.
    /// </summary>
    private async Task<TipoResolvido> ResolverTipoAsync(
        RegistrarAtoNormativoCommand command,
        ParteConferida<string> codigo,
        DateTimeOffset publicadoEm,
        CancellationToken cancellationToken)
    {
        if (!codigo.EstaAceita)
        {
            return TipoResolvido.Ausente;
        }

        if (command.AtributosDoTipo is { } conferidos)
        {
            return TipoResolvido.Resolvido(TipoConferidoPeloDominio(codigo.Valor!, conferidos));
        }

        Result<DateOnly> diaDaPublicacao = fuso.DiaCivilDe(publicadoEm);
        return diaDaPublicacao.IsFailure
            ? TipoResolvido.Recusado(diaDaPublicacao.Errors)
            : await ResolverNoCatalogoAsync(codigo.Valor!, diaDaPublicacao.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TipoResolvido> ResolverNoCatalogoAsync(
        string tipoCodigo, DateOnly diaDaPublicacao, CancellationToken cancellationToken)
    {
        TipoAtoPublicado? tipo = await tipos
            .ObterVigenteAsync(tipoCodigo, diaDaPublicacao, cancellationToken)
            .ConfigureAwait(false);

        return tipo is null
            ? TipoResolvido.Recusado([TipoSemVersaoVigente(diaDaPublicacao)])
            : TipoResolvido.Resolvido(TipoDoAtoNoRegistro.CopiadoDe(tipo));
    }

    private static FieldError TipoSemVersaoVigente(DateOnly diaDaPublicacao) =>
        new(
            "tipoCodigo",
            new DomainError(
                AtoNormativoErrorCodes.TipoSemVersaoVigente,
                $"O tipo de ato informado não tem versão cadastrada que valha na data da publicação ({diaDaPublicacao:yyyy-MM-dd})."));

    private static TipoDoAtoNoRegistro TipoConferidoPeloDominio(string codigo, AtributosDoTipoAto atributos) =>
        new(codigo, atributos.Nome, atributos.CongelaConfiguracao, atributos.EfeitoIrreversivel, atributos.UnicoPorObjeto);

    /// <summary>
    /// Quando o registro emenda outro ato, as invariantes da cadeia que dependem do ato retificado
    /// são verificadas com o retificado em mãos (ADR-0103). Os vínculos herdados só se conhecem
    /// depois de achar o ato retificado: uma retificação que não repete o vínculo o herda dele, e
    /// nasceria sem documento do mesmo jeito. Só a retificação aceita na forma e com ato retificado
    /// preenchido lê o banco.
    /// </summary>
    private async Task<RetificacaoLida> LerRetificacaoAsync(
        RegistrarAtoNormativoCommand command,
        ParteConferida<RelacaoDeRetificacao> retificacao,
        TipoDoAtoNoRegistro? tipo,
        CancellationToken cancellationToken)
    {
        if (retificacao is not { EstaAceita: true, Valor.AtoRetificadoId: { } retificadoId })
        {
            return RetificacaoLida.Nenhuma;
        }

        IReadOnlyList<FieldError> daCadeia = await cadeia.ConferirAsync(retificadoId, tipo, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EntidadeVinculada> herdados = CadeiaDeRetificacaoDoAto.RetificadoExiste(daCadeia)
            ? await cadeia.ListarVinculosHerdadosAsync(retificadoId, cancellationToken).ConfigureAwait(false)
            : [];

        return new RetificacaoLida([.. daCadeia, .. RecusaDaHeranca(command.ChegouPeloDominioDono, herdados)], herdados);
    }

    private static IEnumerable<FieldError> RecusaDaHeranca(bool chegouPeloDominioDono, IEnumerable<EntidadeVinculada> herdados) =>
        !chegouPeloDominioDono && herdados.Any(vinculo => EntidadesDeRegistroPeloDominio.Contem(vinculo.Tipo))
            ? [RegistroPeloDominioExigido("atoRetificadoId")]
            : [];

    /// <summary>
    /// O ato que trata de objeto cujos atos pertencem ao domínio dele não nasce pelo endpoint: este
    /// registro não recebe o arquivo, e o ato ficaria sem documento. A recusa aponta o tipo da
    /// entidade do vínculo, sem escrever o valor recebido.
    /// </summary>
    private static IEnumerable<FieldError> RecusaDoVinculoExclusivoDoDominio(
        bool chegouPeloDominioDono, string campo, EntidadeVinculada? vinculo) =>
        !chegouPeloDominioDono && vinculo is { } declarado && EntidadesDeRegistroPeloDominio.Contem(declarado.Tipo)
            ? [RegistroPeloDominioExigido(campo)]
            : [];

    private static FieldError RegistroPeloDominioExigido(string campo) =>
        new(campo, new DomainError(AtoNormativoErrorCodes.VinculoExigeRegistroPeloDominio, RegistroPeloDominioExigidoMensagem));

    /// <summary>
    /// Todas as recusas do pedido, campo a campo na ordem do corpo: numeração, código do tipo (forma ou
    /// tipo sem versão vigente), data do ato, documento, versão do objeto, ato retificado (forma, cadeia e herança),
    /// motivo, cada vínculo (tipo da entidade, registro pelo domínio dono, identificador, repetição) e
    /// anexos.
    /// </summary>
    private static IReadOnlyList<FieldError> NaOrdemDoPedido(
        RegistrarAtoNormativoCommand command,
        DeclaracaoDoAtoNormativo declaracao,
        ConferenciaDaDeclaracaoDoAto forma,
        TipoResolvido tipo,
        RetificacaoLida retificacao) =>
    [
        .. forma.Numeracao.Recusas,
        .. forma.CodigoDoTipo.Recusas,
        .. tipo.Recusas,
        .. forma.DataDoAto.Recusas,
        .. forma.Documento.Recusas,
        .. forma.VersaoDoObjeto.Recusas,
        .. forma.Retificacao.Recusas,
        .. retificacao.Recusas,
        .. forma.Vinculos.SelectMany((parte, posicao) =>
            RecusasDoVinculo(command.ChegouPeloDominioDono, parte, posicao, declaracao.Vinculos[posicao])),
        .. forma.RecusasDosAnexos,
    ];

    private static IEnumerable<FieldError> RecusasDoVinculo(
        bool chegouPeloDominioDono, ParteConferida<EntidadeVinculada> parte, int posicao, EntidadeVinculada? declarado)
    {
        string campoDoTipo = $"vinculos[{posicao}].entidadeTipo";
        return
        [
            .. parte.Recusas.Where(recusa => recusa.Field == campoDoTipo),
            .. RecusaDoVinculoExclusivoDoDominio(chegouPeloDominioDono, campoDoTipo, declarado),
            .. parte.Recusas.Where(recusa => recusa.Field != campoDoTipo),
        ];
    }

    private async Task<Result<AtoMontado>> MontarSemRecusaAsync(
        RegistrarAtoNormativoCommand command,
        DateTimeOffset publicadoEm,
        ConferenciaDaDeclaracaoDoAto forma,
        TipoDoAtoNoRegistro tipo,
        IReadOnlyList<EntidadeVinculada> herdados,
        CancellationToken cancellationToken)
    {
        Guid? retificadoId = forma.Retificacao.Valor.AtoRetificadoId;
        LinhagemDoAto linhagem = await cadeia
            .ResolverAsync(retificadoId, [.. forma.Vinculos.Select(vinculo => vinculo.Valor)], herdados, cancellationToken)
            .ConfigureAwait(false);
        AtoNormativo ato = Criar(command, publicadoEm, forma, tipo, linhagem);
        DomainError? recusa = await ReservarAsync(retificadoId, ato, linhagem, cancellationToken).ConfigureAwait(false);

        return recusa is null
            ? Result<AtoMontado>.Success(new AtoMontado(ato, linhagem.IdsDaPropriaCadeia))
            : Result<AtoMontado>.Failure(recusa);
    }

    /// <summary>
    /// O id vem do domínio que publicou, quando o registro chega pela porta de escrita (ADR-0170).
    /// No caminho HTTP não há domínio remoto a quem ele pertença: <see cref="Guid.Empty"/> faz a
    /// factory gerá-lo.
    /// </summary>
    private static AtoNormativo Criar(
        RegistrarAtoNormativoCommand command,
        DateTimeOffset publicadoEm,
        ConferenciaDaDeclaracaoDoAto forma,
        TipoDoAtoNoRegistro tipo,
        LinhagemDoAto linhagem) =>
        AtoNormativo.Registrar(
            command.AtoId ?? Guid.Empty,
            EssenciaDeclarada(command, forma, tipo),
            publicadoEm,
            forma.VersaoDoObjeto.Valor,
            linhagem.Vinculos,
            [.. (command.Anexos ?? []).Select(anexo => (anexo.Nome!, anexo.TipoMidia!, anexo.Hash!))]);

    private static EssenciaDocumentalDoAto EssenciaDeclarada(
        RegistrarAtoNormativoCommand command, ConferenciaDaDeclaracaoDoAto forma, TipoDoAtoNoRegistro tipo) =>
        new(
            forma.Numeracao.Valor,
            tipo,
            forma.DataDoAto.Valor,
            forma.Documento.Valor.Hash,
            forma.Documento.Valor.Assinante,
            forma.Retificacao.Valor);

    private async Task<DomainError?> ReservarAsync(
        Guid? retificadoId, AtoNormativo ato, LinhagemDoAto linhagem, CancellationToken cancellationToken)
    {
        Guid raiz = await cadeia.ObterRaizAsync(retificadoId, ato.Id, cancellationToken).ConfigureAwait(false);
        return await reservaDasVagas
            .ReservarAsync(ato, linhagem.IdsDaPropriaCadeia, raiz, cancellationToken).ConfigureAwait(false);
    }

    private sealed record TipoResolvido(TipoDoAtoNoRegistro? Tipo, IReadOnlyList<FieldError> Recusas)
    {
        public static TipoResolvido Ausente { get; } = new(null, []);

        public static TipoResolvido Resolvido(TipoDoAtoNoRegistro tipo) => new(tipo, []);

        public static TipoResolvido Recusado(IReadOnlyList<FieldError> recusas) => new(null, recusas);
    }

    private sealed record RetificacaoLida(IReadOnlyList<FieldError> Recusas, IReadOnlyList<EntidadeVinculada> Herdados)
    {
        public static RetificacaoLida Nenhuma { get; } = new([], []);
    }
}

/// <summary>
/// O ato montado, ainda não gravado, e os ids da cadeia de retificação que ele integra: é com
/// eles que se distingue a colisão de numeração da republicação dentro da mesma linhagem.
/// </summary>
public sealed record AtoMontado(AtoNormativo Ato, IReadOnlyList<Guid> IdsDaPropriaCadeia);
