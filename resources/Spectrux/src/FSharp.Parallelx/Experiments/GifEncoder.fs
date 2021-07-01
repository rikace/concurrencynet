namespace FSharp.Parallelx.Experiments

module GifEncoder =
  open System
  open System.Collections.Generic
  
  
  type Entry =
        | I8 of int
        | I16 of int
        | String of string
        | Var of byte []
            
  
  type Color =
        { Red: int
          Green: int
          Blue: int }  
  
  type Palette =
        { Background: int
          Bits: int
          Table: Color array }  
  
  let palette background (table: _ []) =
        let bits = log(float table.Length) / log 2.0 |> truncate |> int
        { Background=background; Bits=bits; Table=table }
  
  type GIF =
        { Width: int
          Height: int
          Palette: Palette option
          Transparent: int option
          FramesPerSecond: int
          Reps: int
          Frames: (string * byte []) list }  
  
  let compress bits (input: byte []) =
        let output = ResizeArray()
        let count = 1 <<< bits
        let clearCode = count
        let endOfInfo = count + 1
        let mutable codeSize = 0
        let mutable buffer = 0
        let mutable length = 0
        let table = Dictionary<int * int, int> HashIdentity.Structural
        let mutable nextCode = 0
        let append n =
          buffer <- buffer ||| ((n &&& (1 <<< codeSize)-1) <<< length)
          length <- length + codeSize
          while length >= 8 do
            output.Add(byte buffer)
            buffer <- buffer >>> 8
            length <- length - 8
        let reset() =
          table.Clear()
          for i = 0 to count - 1 do
            table.[(-1, i)] <- i
          codeSize <- bits + 1
          nextCode <- count + 2
        reset()
        append clearCode
        let encodeByte prefix b =
          let k = int b
          let mutable v = Unchecked.defaultof<_>
          if table.TryGetValue((prefix, k), &v) then v else
            append prefix
            table.[(prefix, k)] <- nextCode
            if nextCode = (1 <<< codeSize) then
              codeSize <- codeSize + 1  
            nextCode <- nextCode + 1
            if nextCode = 0x1000 then
              append clearCode
              reset()
            k
        append (Array.fold encodeByte -1 input)    
        append endOfInfo    
        if length > 0 then
          output.Add(byte(buffer &&& (1 <<< length)-1))
        output.ToArray()
            
            
            
  let colorTable palette =
        palette.Table
        |> Seq.collect (fun color -> Seq.map I8 [color.Red; color.Green; color.Blue])
        
  let logicalScreen gif =
        [ yield I16 gif.Width
          yield I16 gif.Height
          match gif.Palette with
          | None -> yield! Seq.map I8 [0; 0; 0] (* default aspect ratio (1:1 = 49) *)
          | Some palette ->
              yield! Seq.map I8 [0xf0 ||| palette.Bits-1; palette.Background; 0]
              yield! colorTable palette ]
  let graphicsControlExtension {Transparent=transparent; FramesPerSecond=fps} =
        [ yield! [I8 0x21; I8 0xf9; I8 0x04 ]
          yield I8(match transparent with None -> 0 | Some _ -> 9)
          yield I16(if fps = 0 then 0 else (200 + fps) / (fps + fps))
          yield I8(match transparent with None -> 0 | Some c -> c)
          yield I8 0 ]
        
  let imageDescriptor x y gif =
        [ yield! [ I8 0x2c; I16 x; I16 y; I16 gif.Width; I16 gif.Height ]
          match gif.Palette with
          | None -> yield I8 0
          | Some palette ->
              yield I8(0x80 ||| palette.Bits-1)
              yield! colorTable palette ]
        
  let image bytes gif =
        let bits =
          match gif.Palette with
          | Some palette -> max 2 palette.Bits
          | None -> failwith "image"
        [ match gif.Transparent, gif.FramesPerSecond with
          | None, 0 -> ()
          | _   , _ -> yield! graphicsControlExtension gif
          yield! imageDescriptor 0 0 gif
          yield I8 bits
          yield Var(compress bits bytes) ]
        
  let grayscale =
        [|for i in 0..255 -> { Red = i; Green = i; Blue = i }|]
        |> palette 0
        
  let comment (text: string) =
        [ I8 0x21; I8 0xfe; Var(System.Text.ASCIIEncoding.ASCII.GetBytes text) ]
  
  let repeat count =
        [ I8 0x21; I8 0xff; I8 0x0b; String "NETSCAPE2.0"; I8 3; I8 1; I16 count; I8 0 ]
        
  let encode gif =
        let entries =
          [ yield String "GIF89a"
            yield! logicalScreen gif
            yield! repeat gif.Reps
            for text, pixels in gif.Frames do
              yield! comment text
              yield! image pixels gif
            yield I8 0x3b ]        
        let output = ResizeArray()
        for entry in entries do
          match entry with
          | I8 b -> output.Add(byte b)
          | I16 n -> Seq.iter output.Add [byte n; byte(n >>> 8)]
          | String text ->
              System.Text.ASCIIEncoding.ASCII.GetBytes text
              |> Seq.iter output.Add
          | Var bytes ->
              let rec loop start =
                let length = min 255 (bytes.Length - start)
                output.Add(byte length)
                for i in start .. start+length-1 do
                  output.Add bytes.[i]
                if length > 0 then loop (start+length)
              loop 0
        output.ToArray()              
              
              
//  let quasicrystal nwaves freq nsteps width height fname =
//        let pi = System.Math.PI
//        let ds = 1.0 / float (max width height)
//        let omega = 2.0 * pi * float freq
//        { Width = width
//          Height = height
//          Palette = Some grayscale
//          Transparent = None
//          FramesPerSecond = 25
//          Reps = 0
//          Frames =
//            [ for p in 0 .. nsteps-1 do
//                sprintf "Frame %d/%d" (p+1) nsteps,
//                let phase = float p * 2.0 * pi / float nsteps                
//                let fs =
//                  { for y in 0 .. height-1 do
//                    let y = ds * float(y - height/2)
//                    for x in 0 .. width-1 do
//                        let x = ds * float(x - width/2)
//                        let mutable s = 0.0
//                        for k in 0 .. nwaves-1 do
//                          let t = pi / float nwaves * float k
//                          let b = y * cos t - x * sin t
//                          s <- s + 0.5 * (cos (b * omega + phase) + 1.0)
//                        s <- s - 2.0 * floor (0.5 * s)
//                        yield byte(255.0 * if s < 1.0 then s else 2.0 - s) }
//                yield fs |> Seq.toArray
//                
//            ] }
//        |> encode
//        |> fun bytes -> System.IO.File.WriteAllBytes(fname, bytes)