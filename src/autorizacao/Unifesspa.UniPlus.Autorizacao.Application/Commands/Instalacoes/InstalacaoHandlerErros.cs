namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros que os handlers da instalação produzem por si: os que decorrem do estado do repositório e do token.</summary>
internal static class InstalacaoHandlerErros
{
    public static DomainError NaoEncontrada() =>
        new(InstalacaoErrorCodes.NaoEncontrada, "Instalação não encontrada.");

    public static DomainError AutorNaoIdentificado() =>
        new(InstalacaoErrorCodes.AutorNaoIdentificado,
            "Não foi possível identificar quem executa a operação; a instalação não foi registrada.");

    public static DomainError JaHaGestor() =>
        new(InstalacaoErrorCodes.JaHaGestor,
            "Já existe gestor de acessos ativo: a instalação só vale para um ambiente sem nenhum.");

    // A recusa de quem inicia não grava nada: a resposta não pode ficar guardada pela chave de idempotência.
    public static DomainError JaHaGestorAoIniciar() =>
        new(InstalacaoErrorCodes.JaHaGestorAoIniciar,
            "Já existe gestor de acessos ativo: a instalação só vale para um ambiente sem nenhum.");

    public static DomainError EmailNaoInstitucional() =>
        new(InstalacaoErrorCodes.EmailNaoInstitucional,
            "O código só é enviado a e-mail institucional verificado, e o e-mail desta sessão não é.");

    // A mesma recusa, com a mesma mensagem, para cada motivo: quem tenta não descobre se há instalação pendente.
    public static DomainError CodigoInvalido() =>
        new(InstalacaoErrorCodes.CodigoInvalido, "O código informado não é válido. Se preciso, inicie a instalação de novo.");

    public static DomainError ConflitoDeConcorrencia() =>
        new(InstalacaoErrorCodes.ConflitoDeConcorrencia,
            "A instalação foi alterada por outra operação. Consulte o estado atual antes de repetir.");
}
