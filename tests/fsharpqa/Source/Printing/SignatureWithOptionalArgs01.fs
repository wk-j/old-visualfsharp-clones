// #Regression #NoMT #Printing #RequiresPowerPack 
// Regression test for FSHARP1.0:1110
// pretty printing signatures with optional arguments

//<Expects status=success>type AsyncTimer =</Expects>
//<Expects status=success>  class</Expects>
//<Expects status=success>    new : f:\(unit -> unit\) \* \?delay:int -> AsyncTimer</Expects>
//<Expects status=success>    member Start : unit -> unit</Expects>
//<Expects status=success>    member Stop : unit -> unit</Expects>
//<Expects status=success>    member Delay : int option</Expects>
//<Expects status=success>    member Delay : int option with set</Expects>
//<Expects status=success>  end</Expects>

open Microsoft.FSharp.Control
 
type AsyncTimer(f, ?delay ) =
  let mutable does_again = true
  let mutable delay = delay
  
  member t.Delay 
    with get() = delay
    and set(v) = lock t (fun _ -> delay <- v)
 
  member t.Start() =
    let rec run() =
      async
        { let curr_does_again = ref false
          let curr_delay = ref None
          do lock t (fun _ -> 
                curr_does_again := does_again 
                curr_delay := t.Delay
              )
          if !curr_does_again then
            match !curr_delay with
            | Some d -> 
                do f()
                do! Async.Sleep d
                return! run()
            | None -> return ()
        }    
    Async.Start(run())
    
  member t.Stop() =  lock t (fun _ -> does_again <- false)

;;

exit 0;;
