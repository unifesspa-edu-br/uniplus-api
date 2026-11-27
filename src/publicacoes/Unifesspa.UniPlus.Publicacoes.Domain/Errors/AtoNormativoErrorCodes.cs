namespace Unifesspa.UniPlus.Publicacoes.Domain.Errors;

public static class AtoNormativoErrorCodes
{
    /// <summary>
    /// O tipo informado não tem versão do catálogo vigente na data da publicação. Sem versão vigente não há de onde copiar <c>congela_configuracao</c>
    /// e <c>efeito_irreversivel</c>, então o registro é recusado.
    /// </summary>
    public const string TipoSemVersaoVigente = "AtoNormativo.TipoSemVersaoVigente";

    /// <summary>
    /// O par <c>{versao_do_objeto_id, versao_do_objeto_hash}</c> veio pela metade —
    /// um identificador sem hash, ou um hash sem identificador. É completo ou ausente.
    /// </summary>
    public const string VersaoDoObjetoIncompleta = "AtoNormativo.VersaoDoObjetoIncompleta";

    /// <summary>O identificador da URL não corresponde a nenhum ato registrado.</summary>
    public const string NaoEncontrado = "AtoNormativo.NaoEncontrado";

    /// <summary>
    /// A retificação referencia um <c>ato_retificado_id</c> que não corresponde a
    /// nenhum ato registrado. Não há linhagem para emendar.
    /// </summary>
    public const string AtoRetificadoNaoEncontrado = "AtoNormativo.AtoRetificadoNaoEncontrado";

    /// <summary>
    /// A classe de congelamento do retificador diverge da do retificado: um ato não
    /// congelante não emenda um congelante, nem o inverso (ADR-0103). É a
    /// <c>congela_configuracao</c> que protege a integridade da configuração
    /// publicada (congelamento dos parâmetros na publicação, UNI-REQ-0019), não o rótulo do tipo.
    /// </summary>
    public const string ClasseDeCongelamentoDivergente = "AtoNormativo.ClasseDeCongelamentoDivergente";

    /// <summary>
    /// O ato que se tentou retificar já foi retificado por outro: a cadeia é linear
    /// e empilha na cabeça (ADR-0103). Uma nova retificação deve emendar a cabeça da
    /// cadeia, não um ato já retificado, seja ele a raiz ou um elo intermediário. A mensagem nomeia o ato que já o retificou.
    /// </summary>
    public const string AtoJaRetificado = "AtoNormativo.AtoJaRetificado";

    /// <summary>
    /// O tipo do ato admite um único ato vivo por objeto (<c>unico_por_objeto</c>), e a
    /// entidade vinculada já é tratada por outra linhagem de atos desse tipo. Não é o
    /// payload que está incoerente — é o estado já gravado que não comporta uma segunda
    /// linhagem; daí 409, e não 422. Uma nova versão do ato vivo é uma retificação da
    /// própria linhagem (ADR-0103), não um ato novo.
    /// </summary>
    public const string ObjetoJaTemAtoVivoDoTipo = "AtoNormativo.ObjetoJaTemAtoVivoDoTipo";

    /// <summary>
    /// O endpoint administrativo recebeu um vínculo com entidade cujos atos só entram
    /// pelo domínio dono do objeto. O endpoint não recebe o arquivo, então o ato nasceria
    /// sem documento e fora das regras do domínio sobre o que pode publicar.
    /// </summary>
    public const string VinculoExigeRegistroPeloDominio = "AtoNormativo.VinculoExigeRegistroPeloDominio";

    /// <summary>
    /// O rótulo do tipo de entidade da consulta não está na grafia canônica (maiúsculas, com grupos
    /// separados por sublinhado). O rótulo chega na rota de uma consulta, não num payload: daí 400, e
    /// não 422. Sem a recusa, a consulta devolveria a mesma coleção vazia de uma entidade sem atos.
    /// </summary>
    public const string TipoDeEntidadeInvalido = "AtoNormativo.TipoDeEntidadeInvalido";

    /// <summary>
    /// Um texto do ato (órgão, série, número, assinante ou motivo da retificação) está vazio quando é
    /// obrigatório, ou passa do tamanho máximo.
    /// </summary>
    public const string TextoInvalido = "AtoNormativo.TextoInvalido";

    /// <summary>O ano da numeração do ato não é maior que zero.</summary>
    public const string AnoInvalido = "AtoNormativo.AnoInvalido";

    /// <summary>O hash do documento não é um SHA-256 em hexadecimal minúsculo.</summary>
    public const string HashDoDocumentoInvalido = "AtoNormativo.HashDoDocumentoInvalido";

    /// <summary>
    /// A retificação é o par (ato retificado, motivo): falta um dos dois lados, ou o identificador do ato
    /// retificado está vazio.
    /// </summary>
    public const string RetificacaoIncompleta = "AtoNormativo.RetificacaoIncompleta";

    /// <summary>O tipo da entidade vinculada está vazio ou fora da forma canônica do rótulo.</summary>
    public const string VinculoTipoDeEntidadeInvalido = "AtoNormativo.VinculoTipoDeEntidadeInvalido";

    /// <summary>O identificador da entidade vinculada está vazio.</summary>
    public const string VinculoSemIdentificadorDaEntidade = "AtoNormativo.VinculoSemIdentificadorDaEntidade";

    /// <summary>A mesma entidade foi vinculada mais de uma vez ao mesmo ato.</summary>
    public const string VinculoRepetido = "AtoNormativo.VinculoRepetido";

    /// <summary>A lista de vínculos traz um elemento nulo.</summary>
    public const string VinculoNulo = "AtoNormativo.VinculoNulo";
}
